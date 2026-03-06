using PlusPim.Debuggers.PlusPimDbg.Program;
using PlusPim.Debuggers.PlusPimDbg.Program.records;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 実行に必要なレジスタ，特殊レジスタ，メモリ情報を提供する
/// </summary>
internal sealed class ExecuteContext(Action<string> log, SymbolTable symbolTable, InstructionIndex startIndex, Label startLabel) {
    /// <summary>
    /// 汎用レジスタの表現
    /// </summary>
    public RegisterFile Registers { get; } = new RegisterFile();

    /// <summary>
    /// プログラムカウンタ
    /// </summary>
    public InstructionIndex PC { get; set; } = startIndex;

    /// <summary>
    /// HIレジスタ
    /// </summary>
    public int HI { get; set; }

    /// <summary>
    /// LOレジスタ
    /// </summary>
    public int LO { get; set; }

    /// <summary>
    /// メモリ空間の表現
    /// アクセス前は未初期化(0扱い)
    /// </summary>
    private readonly Dictionary<Address, byte> _memory = [];

    // これより下のフィールドはデバッグのための追加情報

    /// <summary>
    /// 現在実行中の命令に属すると考えられるラベル
    /// </summary>
    public Label CurrentLabel { get; private set; } = startLabel;

    /// <summary>
    /// コールスタックの表現
    /// </summary>
    public Stack<StackFrame> CallStack { get; } = new();

    /// <summary>
    /// ラベル名からラベルを解決する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <returns>ラベル</returns>
    public Label? ResolveLabelName(string name) {
        return symbolTable.Resolve(name);
    }

    /// <summary>
    /// DataSegmentのメモリイメージをメモリに書き込む
    /// </summary>
    public void LoadDataSegment(DataSegment dataSegment) {
        foreach(KeyValuePair<Address, byte> kvp in dataSegment.MemoryImage) {
            this._memory[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// コールスタックにスタックフレームを追加する
    /// </summary>
    /// <param name="label">次に実行することになるラベル</param>
    public void PushCallStack(Label label) {
        this.CallStack.Push(new StackFrame(this.PC, this.CurrentLabel, this.Registers.Clone(), this.HI, this.LO));
        this.CurrentLabel = label;
    }

    /// <summary>
    /// コールスタックからpopを試みる
    /// </summary>
    /// <param name="jumpTo">ジャンプ先の命令インデックス</param>
    /// <returns>popされたスタックフレーム．ジャンプ先がスタックフレームのPC+1と一致しない場合はnull</returns>
    /// <remarks>ジャンプ先がスタックフレームのPC+1と一致する場合にのみpopする</remarks>
    public StackFrame? TryPopCallStack(InstructionIndex jumpTo) {
        if(this.CallStack.Count > 0) {
            StackFrame frame = this.CallStack.Peek();
            // ジャンプ先がスタックフレームのPC+1と一致するか確認
            if(frame.CurrentPC + 1 == jumpTo) {
                // 実行中のサブルーチンのラベルをスタックフレームのものに戻す
                this.CurrentLabel = frame.Label;
                return this.CallStack.Pop();
            }
        }
        return null;
    }

    /// <summary>
    /// Undo等のための無条件のコールスタックへのプッシュ
    /// </summary>
    /// <param name="frame">スタックフレーム</param>
    public void PushCallStack(StackFrame frame) {
        this.CallStack.Push(frame);
    }


    public byte ReadMemoryByte(Address address) {
        return this._memory.TryGetValue(address, out byte value) ? value : (byte)0;
    }

    public void WriteMemoryByte(Address address, byte value) {
        this._memory[address] = value;
    }

    /// <summary>
    /// 最も基礎的なログ機能．EditorController経由で出力される
    /// </summary>
    public void Log(string message) {
        log.Invoke(message);
    }
}
