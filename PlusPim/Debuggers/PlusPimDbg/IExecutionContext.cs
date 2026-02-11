namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 実行に必要なレジスタ，特殊レジスタ，メモリ情報を提供する
/// </summary>
internal sealed class ExecuteContext {
    /// <summary>
    /// 汎用レジスタの表現
    /// </summary>
    public RegisterFile Registers { get; }

    /// <summary>
    /// プログラムカウンタ
    /// </summary>
    public ProgramCounter PC { get; set; }

    /// <summary>
    /// HIレジスタ
    /// </summary>
    public int HI { get; set; }

    /// <summary>
    /// LOレジスタ
    /// </summary>
    public int LO { get; set; }


    public Stack<CallStackFrame> CallStack { get; } = new();

    private SymbolTable? _symbolTable;

    public void SetSymbolTable(SymbolTable symbolTable) {
        this._symbolTable = symbolTable;
    }

    public int? GetLabelExecutionIndex(string label) {
        return this._symbolTable?.Resolve(label);
    }

    public string? GetLabelForExecutionIndex(int index) {
        return this._symbolTable?.FindByIndex(index);
    }

    /// <summary>
    /// メモリ空間の表現
    /// アクセス前は未初期化(0扱い)
    /// </summary>
    private readonly Dictionary<int, byte> Memory;

    /// <summary>
    /// Log出力用コールバック
    /// </summary>
    private readonly Action<string>? _log;
    public ExecuteContext(Action<string>? log = null) {
        this._log = log;
        this.Registers = new RegisterFile();

        this.PC = ProgramCounter.FromIndex(0);

        // メモリは暗黙的には0扱い
        this.Memory = [];
    }

    public byte ReadMemoryByte(int address) {
        return this.Memory.TryGetValue(address, out byte value) ? value : (byte)0;
    }

    public void WriteMemoryByte(int address, byte value) {
        this.Memory[address] = value;
    }

    /// <summary>
    /// 最も基礎的なログ機能．EditorController経由で出力される
    /// </summary>
    public void Log(string message) {
        this._log?.Invoke(message);
    }
}
