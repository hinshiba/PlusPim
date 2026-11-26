using PlusPim.Image;
using System.Buffers;
using System.Diagnostics;

namespace PlusPim.Dbg;

/// <summary>
/// ある位置で止まるかの判定
/// </summary>
/// <returns>止まらないなら <see langword="null"/></returns>
public delegate T? Probe<T>(in Core c) where T : struct;

/// <summary>
/// 探索の1区間の結果
/// </summary>
public enum Scan: byte {
    /// <summary>判定が値を返した位置にいる</summary>
    Hit,
    /// <summary>区間を探し終えて見つからなかった</summary>
    Boundary,
    /// <summary>入力待ちで進めない. <see cref="Timeline.Forward"/> のみ</summary>
    NeedInput,
    /// <summary>履歴の先頭にいて戻れない. <see cref="Timeline.Backward"/> のみ</summary>
    Start,
}

/// <summary>
/// 履歴を持つデバッギ
/// </summary>
public sealed class Timeline {
    /// <summary>チェックポイントの間隔</summary>
    private const int Interval = 4096;

    private readonly Debuggee _debuggee;
    /// <summary>デバッギの出力先</summary>
    private readonly IBufferWriter<byte> _output;

    /// <summary>チェックポイントごとの状態</summary>
    /// <remarks>書き換えないこと. 0番目は初期状態で index * <see cref="Interval"/> の状態を保持している</remarks>
    private readonly List<State> _checkpoints;

    /// <summary>近傍の先頭の位置. <see cref="Interval"/> の倍数</summary>
    private long _base;

    private int _count;

    /// <summary>現在位置</summary>
    private long Position => this._base + this._count;

    /// <summary>到達した最大の位置</summary>
    private long _frontier;

    /// <summary>現在位置の近傍の状態</summary>
    /// <remarks>
    /// [i] は位置 <see cref="_base"/> + i の印. 有効なのは <see cref="_count"/> 個で, 現在位置は含まない.
    /// 区間の境界の位置は (境界, 0個) と (前の境界, 満杯) のどちらでも表せる.
    /// 現在の状態のメモリの記録は <see cref="_base"/> の位置から始まる.
    /// 毎ステップ操作するので複製と消去を避けて固定長の配列を上書きする.
    /// </remarks>
    private readonly Mark[] _near = new Mark[Interval];

    /// <summary>現在の状態のビュー</summary>
    public StateView View => this._debuggee.View;

    private Timeline(Debuggee debuggee, IBufferWriter<byte> output) {
        this._debuggee = debuggee;
        this._output = output;
        this._checkpoints = [debuggee.State.Fork()];
    }

    /// <summary>プログラムイメージを読み込む</summary>
    /// <param name="image">読み込むプログラムイメージ</param>
    /// <param name="slots">有効にする遅延スロットの種類</param>
    /// <param name="output">デバッギの出力先</param>
    public static Timeline Load(ProgramImage image, DelaySlotsConfig slots, IBufferWriter<byte> output) {
        return new Timeline(Debuggee.Load(image, slots), output);
    }

    /// <summary>
    ///前方向の区間に目的の状態があるかを走査する
    /// </summary>
    /// <param name="judge">進んだ後の状態を判定する. 停止していて進めないときも呼ぶ</param>
    /// <param name="hit"><see cref="Scan.Hit"/> なら判定の値</param>
    /// <returns><see cref="Scan.Hit"/>, <see cref="Scan.NeedInput"/>, または <see cref="Interval"/> ステップ進んだら <see cref="Scan.Boundary"/></returns>
    /// <remarks>現在の状態が最も近いスナップショットなので, そこから進む.</remarks>
    public Scan Forward<T>(Probe<T> judge, out T hit) where T : struct {
        for(int n = 0; n < Interval; n++) {
            if(this.Advance() == StepResult.NeedInput) {
                hit = default;
                return Scan.NeedInput;
            }
            if(judge(in this._debuggee.State.Core) is T t) {
                hit = t;
                return Scan.Hit;
            }
        }
        hit = default;
        return Scan.Boundary;
    }

    /// <summary>
    /// 後ろへ戻り, 現在位置より前で <paramref name="judge"/> が値を返す最後の位置で止まる
    /// </summary>
    /// <param name="judge">各位置の状態を判定する. 新しい位置から順に呼ぶ</param>
    /// <param name="hit"><see cref="Scan.Hit"/> なら判定の値</param>
    /// <returns>
    /// <see cref="Scan.Hit"/>, 履歴の先頭なら <see cref="Scan.Start"/>,
    /// 近傍の区間に見つからなければその先頭へ移って <see cref="Scan.Boundary"/>
    /// </returns>
    /// <remarks>近傍が空なら, 直前のチェックポイントから区間を再実行して近傍にする.</remarks>
    public Scan Backward<T>(Probe<T> judge, out T hit) where T : struct {
        if(this._count == 0) {
            if(this._base == 0) {
                hit = default;
                return Scan.Start;
            }
            this.Rebuild();
        }
        // 印は値なので, 状態を動かさずに判定して1回だけ巻き戻す
        for(int i = this._count - 1; 0 <= i; i--) {
            if(judge(in this._near[i].Core) is T t) {
                this.Seek(i);
                hit = t;
                return Scan.Hit;
            }
        }
        this.Seek(0);
        hit = default;
        return Scan.Boundary;
    }

    /// <summary>入力を末尾に加える. 戻っても入力は失われない</summary>
    public void Feed(ReadOnlySpan<byte> input) {
        this._debuggee.Feed(input);
    }

    /// <summary>EOFを入力する</summary>
    public void CloseInput() {
        this._debuggee.CloseInput();
    }

    /// <summary>
    /// 1ステップ進め近傍の状態を追加する.
    /// </summary>
    /// <remarks>
    /// すべての実行はここを通るので, 出力が位置ごとに一度だけ書かれることとチェックポイントの追加はここだけで決まる.
    /// 区間の末尾でもまだ戻れるよう, 次の区間へ移るのは離れるときにする.
    /// </remarks>
    private StepResult Advance() {
        State s = this._debuggee.State;
        if(this._count == Interval) {
            this._base += Interval;
            this._count = 0;
            s.Memory.Commit();
            if(this._checkpoints.Count * (long)Interval <= this._base) {
                this._checkpoints.Add(s.Fork());
            }
        }
        bool fresh = this._frontier <= this.Position;
        this._near[this._count] = s.Save();
        StepResult r = this._debuggee.Step(fresh ? this._output : Discard.Instance);
        if(r == StepResult.Stepped) {
            this._count++;
            if(fresh) {
                this._frontier = this.Position;
            }
        }
        return r;
    }

    /// <summary>
    /// 直前の区間をチェックポイントから再実行して近傍にする.
    /// 現在位置はその区間の終わりで変化しない.
    /// </summary>
    private void Rebuild() {
        this._base -= Interval;
        this._debuggee.State = this._checkpoints[(int)(this._base / Interval)].Fork();
        while(this._count < Interval) {
            // 到達点より前なので出力は捨てられ, 記録済みの入力を読むので入力待ちにもならない
            if(this.Advance() != StepResult.Stepped) {
                throw new UnreachableException("Replay diverged from the recorded execution.");
            }
        }
    }

    /// <summary>近傍の <paramref name="i"/> 番目の位置へ戻る. 戻る量に比例した時間がかかる</summary>
    private void Seek(int i) {
        this._debuggee.State.Rewind(in this._near[i]);
        this._count = i;
    }
}
