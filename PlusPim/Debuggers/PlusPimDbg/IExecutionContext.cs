namespace PlusPim.Debuggers.PlusPimDbg;

/// <summary>
/// 実行のためのすべてのコンテキスト
/// </summary>
/// <remarks>
/// - レジスタファイル
/// - 特殊レジスタ(PC, HI, LO)
/// - 次の命令インデックス
/// - コールスタック
/// - メモリ空間
/// </remarks>
internal interface IExecutionContext {
    /// <summary>
    /// レジスタIDに対応するレジスタ値の配列
    /// </summary>
    int[] Registers { get; }

    /// <summary>
    /// プログラムカウンタだが，ExecutionIndexから自動算出されるだけ
    /// </summary>
    int PC { get; }

    /// <summary>
    /// プログラムカウンタの代わり
    /// </summary>
    int ExecutionIndex { get; set; }

    /// <summary>
    /// HIレジスタ
    /// </summary>
    int HI { get; set; }

    /// <summary>
    /// LOレジスタ
    /// </summary>
    int LO { get; set; }
    byte ReadMemoryByte(int address);
    void WriteMemoryByte(int address, byte value);

    /// <summary>
    /// ログを返すためのメソッド
    /// </summary>
    /// <param name="message">送信文字列</param>
    void Log(string message);
}

/// <summary>
/// 実行に必要なレジスタ，特殊レジスタ，メモリ情報を提供する
/// </summary>
internal sealed class ExecuteContext: IExecutionContext {
    public const int TextSegmentBase = 0x00400000;

    public int[] Registers { get; }
    public int PC => this.ExecutionIndex + TextSegmentBase;

    /// PCの実装の代わり
    public int ExecutionIndex { get; set; }

    public int HI { get; set; }
    public int LO { get; set; }

    /// <summary>
    /// メモリ空間の表現
    /// アクセス前は未初期化(0扱い)
    /// </summary>
    private readonly Dictionary<int, byte> Memory;
    private readonly Action<string>? _log;
    public ExecuteContext(Action<string>? log = null) {
        this._log = log;
        this.Registers = new int[32];
        // 未初期化のうほうが現実的
        //Array.Clear(this.Registers, 0, 32);
        // HI LO も同様

        // PCの代わりのExecutionIndexは初期化
        this.ExecutionIndex = 0;

        // メモリは暗黙的には0扱い
        this.Memory = [];
    }

    public byte ReadMemoryByte(int address) {
        return this.Memory.TryGetValue(address, out byte value) ? value : (byte)0;
    }

    public void WriteMemoryByte(int address, byte value) {
        this.Memory[address] = value;
    }

    public void Log(string message) {
        this._log?.Invoke(message);
    }
}
