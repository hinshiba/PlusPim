using PlusPim.Debuggers.PlusPimDbg.Program.records;
using System.Buffers.Binary;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 実行に必要なレジスタ，特殊レジスタ，メモリ情報を提供する
/// </summary>
internal sealed class RuntimeContext(Action<string> log, Func<string, Address, bool, Label?> resolveLabel, Address startAddr, Label startLabel) {
    /// <summary>
    /// 汎用レジスタの表現
    /// </summary>
    public RegisterFile Registers { get; private set; } = new RegisterFile();

    /// <summary>
    /// プログラムカウンタ
    /// </summary>
    public Address PC { get; set; } = startAddr;

    /// <summary>
    /// HIレジスタ
    /// </summary>
    public uint HI { get; set; }

    /// <summary>
    /// LOレジスタ
    /// </summary>
    public uint LO { get; set; }

    /// <summary>
    /// メモリ空間の表現
    /// アクセス前は未初期化(0扱い)
    /// </summary>
    private readonly Dictionary<Address, byte> _memory = [];

    // これより下のフィールドはデバッグのための追加情報

    /// <summary>
    /// プログラムの終了の有無
    /// </summary>
    public bool IsTerminated { get; set; } = false;

    /// <summary>
    /// 直前のステップで発生した例外の情報 (nullなら例外なし)
    /// </summary>
    public ExceptionEvent? LastException { get; private set; }

    /// <summary>
    /// 現在実行中の命令に属すると考えられるラベル
    /// </summary>
    public Label CurrentLabel { get; private set; } = startLabel;

    private readonly Stack<StackFrame> _callStack = new();

    /// <summary>
    /// コールスタックの表現
    /// </summary>
    public IReadOnlyCollection<StackFrame> CallStack => this._callStack;


    public bool IsKernelMode => this._cp0Regs.Exl;

    // 例外処理のためのフィールド
    private CP0RegisterFile _cp0Regs = CP0RegisterFile.Default;

    /// <summary>
    /// ラベル名からラベルを解決する
    /// </summary>
    /// <param name="name">ラベル名</param>
    /// <returns>ラベル</returns>
    public Label? ResolveLabelName(string name) {
        return resolveLabel(name, this.PC, this.IsKernelMode);
    }

    /// <summary>
    /// メモリイメージをメモリに書き込む
    /// </summary>
    public void LoadMemoryImage(Dictionary<Address, byte> memoryImage) {
        foreach(KeyValuePair<Address, byte> kvp in memoryImage) {
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
    /// <returns>popされたスタックフレーム．ジャンプ先がスタックフレームのPC+4と一致しない場合はnull</returns>
    /// <remarks>ジャンプ先がスタックフレームのPC+4と一致する場合にのみpopする</remarks>
    public StackFrame? TryPopCallStack(Address jumpTo) {
        if(this.CallStack.Count > 0) {
            StackFrame frame = this._callStack.Peek();
            // ジャンプ先がスタックフレームのPC+4と一致するか確認
            if(frame.CurrentPC + 4 == jumpTo) {
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
    /// <param name="value">書き込む値</param>
    public void WriteMemoryByte(Address address, byte value) {
        this._memory[address] = value;
    }

    /// <summary>
    /// 任意のバイト数のメモリ読み込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <param name="num">読み込むメモリ数</param>
    /// <param name="isSign">符号拡張をするかどうか．<see langword="false"/>ならゼロ拡張</param>
    /// <returns>そのアドレスから任意のバイト数を読み込んで拡張した値</returns>
    public uint ReadMemoryBytes(Address address, int num, bool isSign) {
        if(num is < 1 or > 4) {
            throw new ArgumentOutOfRangeException(nameof(num), "num must be between 1 and 4.");
        }
        Span<byte> bytes = stackalloc byte[4];
        for(int i = 0; i < num; i++) {
            bytes[i] = this.ReadMemoryByte(address++);
        }

        if(isSign) {
            // 符号拡張
            bytes[num..].Fill(
                ((bytes[num - 1] & 0x80) != 0) // 最上位ビットが1かどうか
                    ? (byte)0xFF
                    : (byte)0x00
                ); // 符号拡張
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes); // リトルエンディアンにしてくれるので逆順にする必要なし
    }

    /// <summary>
    /// 任意のバイト数のメモリ書き込み
    /// </summary>
    /// <param name="address">アドレス</param>
    /// <param name="val">書き込む値</param>
    /// <param name="num">書き込むバイト数</param>
    /// <remarks><paramref name="val"/>の下位<paramref name="num"/>バイトを書き込む</remarks>
    public void WriteMemoryBytes(Address address, uint val, int num) {
        if(num is < 1 or > 4) {
            throw new ArgumentOutOfRangeException(nameof(num), "num must be between 1 and 4.");
        }
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, val);
        // リトルエンディアンにしてくれるので逆順にする必要なし
        foreach(byte b in bytes[..num]) {
            this._memory[address++] = b;
        }
    }


    /// <summary>
    /// 最も基礎的なログ機能．EditorController経由で出力される
    /// </summary>
    public void Log(string message) {
        log.Invoke(message);
    }



    /// <summary>
    /// 例外を発生させる
    /// </summary>
    /// <param name="reason">発生理由</param>
    /// <param name="badVAddr">アドレスが関わる場合は，原因となったアドレス</param>
    public void RaiseException(ExcCode reason, Address? badVAddr = null) {
        if(this.IsKernelMode) {
            this.LastException = new ExceptionEvent(reason, IsDouble: true);
            this.Log($"Double exception raised: {reason}. So terminate debugee.");
            this.IsTerminated = true;
            return;
        }

        this.LastException = new ExceptionEvent(reason, IsDouble: false);
        this.Log($"Exception raised: {reason}");

        this._cp0Regs = new CP0RegisterFile {
            BadVAddr = badVAddr,
            Exl = true, // 実質的にカーネル空間のフラグ
            Exc = reason,
            Epc = this.PC
        };
    }

    /// <summary>
    /// 例外を解決する
    /// </summary>
    public void RetException() {
        // EPCの値に復帰
        this.PC = this._cp0Regs.Epc;
        // 最後の例外を消す
        this.LastException = null;
        // カーネルモードから脱出する
        this._cp0Regs = CP0RegisterFile.Default;
    }

    /// <summary>
    /// 最後の例外イベントの情報を消す
    /// </summary>
    /// <returns>消去した場合は<see langword="true"/></returns>
    public bool AckException() {
        if(this.LastException is null) {
            return false;
        }
        this.LastException = null;
        return true;
    }

    /// <summary>
    /// CP0レジスタをMIPS番号で読み取る
    /// </summary>
    public uint ReadCP0Register(int regNum) {
        return regNum switch {
            8 => this._cp0Regs.BadVAddr?.Addr ?? 0,
            12 => this._cp0Regs.Exl ? 0x2u : 0x0u,
            13 => (uint)this._cp0Regs.Exc << 2,
            14 => this._cp0Regs.Epc.Addr,
            _ => 0
        };
    }

    /// <summary>
    /// CP0レジスタをMIPS番号で書き込む
    /// </summary>
    public void WriteCP0Register(int regNum, uint value) {
        this._cp0Regs = regNum switch {
            8 => this._cp0Regs with { BadVAddr = new Address(value) },
            12 => this._cp0Regs with { Exl = (value & 0x2) != 0 },
            13 => this._cp0Regs with { Exc = (ExcCode)((value >> 2) & 0x1F) },
            14 => this._cp0Regs with {
                Epc = new Address(value)
            },
            _ => this._cp0Regs
        };
    }

    /// <summary>
    /// CP0状態のスナップショットを取得する (Undo用)
    /// </summary>
    public CP0RegisterFile GetCP0Snapshot() {
        return this._cp0Regs;
    }

    /// <summary>
    /// CP0状態を復元する (Undo用)
    /// </summary>
    public void RestoreCP0(CP0RegisterFile snapshot) {
        this._cp0Regs = snapshot;
    }

    /// <summary>
    /// CP0レジスタの表示用値を取得する (DAP用)
    /// </summary>
    public (uint BadVAddr, uint Status, uint Cause, uint EPC) GetCP0DisplayValues() {
        return (this.ReadCP0Register(8), this.ReadCP0Register(12), this.ReadCP0Register(13), this.ReadCP0Register(14));
    }
}

/// <summary>
/// ステップ実行中に発生した例外イベントの情報
/// </summary>
internal record struct ExceptionEvent(ExcCode Code, bool IsDouble);
