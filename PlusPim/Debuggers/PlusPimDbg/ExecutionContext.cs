namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 実行に必要なレジスタ，特殊レジスタ，メモリ情報を提供する
/// </summary>
internal sealed class ExecuteContext(Action<string> log, SymbolTable symbolTable) {
    /// <summary>
    /// 汎用レジスタの表現
    /// </summary>
    public RegisterFile Registers { get; } = new RegisterFile();

    /// <summary>
    /// プログラムカウンタ
    /// </summary>
    public ProgramCounter PC { get; set; } = ProgramCounter.FromIndex(0);

    /// <summary>
    /// HIレジスタ
    /// </summary>
    public int HI { get; set; }

    /// <summary>
    /// LOレジスタ
    /// </summary>
    public int LO { get; set; }


    public Stack<CallStackFrame> CallStack { get; } = new();

    private readonly SymbolTable _symbolTable = symbolTable;

    public int? GetLabelExecutionIndex(string label) {
        return this._symbolTable.Resolve(label);
    }

    public string? GetLabelForExecutionIndex(int index) {
        return this._symbolTable.FindByIndex(index);
    }

    /// <summary>
    /// メモリ空間の表現
    /// アクセス前は未初期化(0扱い)
    /// </summary>
    private readonly Dictionary<int, byte> _memory = [];

    /// <summary>
    /// Log出力用コールバック
    /// </summary>
    private readonly Action<string> _log = log;

    public byte ReadMemoryByte(int address) {
        return this._memory.TryGetValue(address, out byte value) ? value : (byte)0;
    }

    public void WriteMemoryByte(int address, byte value) {
        this._memory[address] = value;
    }

    /// <summary>
    /// 最も基礎的なログ機能．EditorController経由で出力される
    /// </summary>
    public void Log(string message) {
        this._log.Invoke(message);
    }
}
