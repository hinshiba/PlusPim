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
    public RegisterFile Registers { get; private set; } = new RegisterFile();

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

    private readonly Stack<StackFrame> _callStack = new();

    /// <summary>
    /// コールスタックの表現
    /// </summary>
    public IReadOnlyCollection<StackFrame> CallStack => this._callStack;

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
        this._callStack.Push(new StackFrame(this.PC, this.CurrentLabel, this.Registers.Clone(), this.HI, this.LO));
        this.CurrentLabel = label;
    }

    /// <summary>
    /// Undo等のための無条件のコールスタックからのポップ
    /// </summary>
    /// <exception cref="InvalidOperationException">コールスタックが空のとき．
    /// これはjal命令のUndoが主目的であり，そのような状況どこかで誤ってpopしている以外で空となるのはありえないため例外</exception>
    public void UndoPushCallStack() {
        if(this.CallStack.Count == 0) {
            throw new InvalidOperationException("Cannot pop from an empty call stack.");
        }
        this.CurrentLabel = this._callStack.Pop().Label;
    }

    /// <summary>
    /// コールスタックからpopを試みる
    /// </summary>
    /// <param name="jumpTo">ジャンプ先の命令インデックス</param>
    /// <returns>popされたスタックフレーム．ジャンプ先がスタックフレームのPC+1と一致しない場合はnull</returns>
    /// <remarks>ジャンプ先がスタックフレームのPC+1と一致する場合にのみpopする</remarks>
    public StackFrame? TryPopCallStack(InstructionIndex jumpTo) {
        if(this.CallStack.Count > 0) {
            StackFrame frame = this._callStack.Peek();
            // ジャンプ先がスタックフレームのPC+1と一致するか確認
            if(frame.CurrentPC + 1 == jumpTo) {
                // 実行中のサブルーチンのラベルをスタックフレームのものに戻す
                this.CurrentLabel = frame.Label;
                return this._callStack.Pop();
            }
        }
        return null;
    }

    /// <summary>
    /// <see cref="TryPopCallStack"/> のUndoのためのプッシュ
    /// </summary>
    /// <param name="label">順方向実行前のラベル</param>
    /// <param name="frame">復元したいスタックフレーム</param>
    public void UndoTryPopCallStack(Label label, StackFrame? frame) {
        this.CurrentLabel = label;
        if(frame != null) {
            this._callStack.Push(frame);
        }
    }


    /// <summary>
    /// 1バイトのメモリ読み込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <returns>そのアドレスの値</returns>
    public byte ReadMemoryByte(Address address) {
        return this._memory.TryGetValue(address, out byte value) ? value : (byte)0;
    }

    /// <summary>
    /// 1バイトのメモリ書き込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <returns>そのアドレスの値</returns>
    public void WriteMemoryByte(Address address, byte value) {
        this._memory[address] = value;
    }

    /// <summary>
    /// 4バイトのメモリ読み込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <returns>そのアドレスから4バイトの値</returns>
    public uint ReadMemoryWord(Address address) {
        byte b0 = this._memory.TryGetValue(address, out byte v0) ? v0 : (byte)0;
        byte b1 = this._memory.TryGetValue(address + 1, out byte v1) ? v1 : (byte)0;
        byte b2 = this._memory.TryGetValue(address + 2, out byte v2) ? v2 : (byte)0;
        byte b3 = this._memory.TryGetValue(address + 3, out byte v3) ? v3 : (byte)0;
        return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
    }

    /// <summary>
    /// 4バイトのメモリ書き込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <returns>そのアドレスから4バイトの値</returns>
    public void WriteMemoryWord(Address address, uint value) {
        this._memory[address] = (byte)(value & 0xFF);
        this._memory[address + 1] = (byte)((value >> 8) & 0xFF);
        this._memory[address + 2] = (byte)((value >> 16) & 0xFF);
        this._memory[address + 3] = (byte)((value >> 24) & 0xFF);
    }


    /// <summary>
    /// 最も基礎的なログ機能．EditorController経由で出力される
    /// </summary>
    public void Log(string message) {
        log.Invoke(message);
    }
}
