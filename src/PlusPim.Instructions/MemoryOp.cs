namespace PlusPim.Instructions;

/// <summary>
/// ロードを抽象化する
/// </summary>
public interface ILoadOp {
    static abstract string Mnemonic { get; }
    static abstract LoadKind Kind { get; }
}

/// <summary>
/// ストアを抽象化する
/// </summary>
public interface IStoreOp {
    static abstract string Mnemonic { get; }
    static abstract StoreKind Kind { get; }
}

/// <summary>
/// ロードの共通部分を提供する
/// </summary>
/// <typeparam name="TOp">ロード</typeparam>
/// <param name="rt">書き込むレジスタ番号</param>
/// <param name="mem">実効アドレスのベースとオフセット</param>
/// <param name="sourceLine">行番号</param>
/// <remarks>
/// <see cref="LoadKind.Left"/> と <see cref="LoadKind.Right"/> の rt の旧値との合成はランタイムが行うため, rt は Src に含めない
/// </remarks>
public sealed class LoadOp<TOp>(GprId rt, Memory mem, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, ILoadOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(rt, mem.BaseRegId);
    private readonly uint _offset = mem.Offset.SignExtended;

    /// <summary>実効アドレスの下位で0であるべきビット. 非アラインを許すなら0</summary>
    private static uint AlignMask => TOp.Kind switch {
        LoadKind.H or LoadKind.HU => 1,
        LoadKind.W => 3,
        _ => 0,
    };

    public ReadOnlySpan<RegisterName> Src => this._regs[1..];
    public ReadOnlySpan<RegisterName> Dest => this._regs[..1];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId t, Memory m] ? new LoadOp<TOp>(t, m, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        Address addr = new(unchecked(src[0] + this._offset));
        return (addr.Addr & AlignMask) != 0
            ? new Effect(new AddressError(addr, Store: false))
            : new Effect(new Load(addr, TOp.Kind));
    }
}

/// <summary>
/// ストアの共通部分を提供する
/// </summary>
/// <typeparam name="TOp">ストア</typeparam>
/// <param name="rt">書き込む値のレジスタ番号</param>
/// <param name="mem">実効アドレスのベースとオフセット</param>
/// <param name="sourceLine">行番号</param>
public sealed class StoreOp<TOp>(GprId rt, Memory mem, int sourceLine): IAssemblerable, IExecute
    where TOp : struct, IStoreOp {

    public static string Mnemonic => TOp.Mnemonic;
    private readonly RegIds2 _regs = RegIds2.Of(rt, mem.BaseRegId);
    private readonly uint _offset = mem.Offset.SignExtended;

    /// <summary>実効アドレスの下位で0であるべきビット. 非アラインを許すなら0</summary>
    private static uint AlignMask => TOp.Kind switch {
        StoreKind.H => 1,
        StoreKind.W => 3,
        _ => 0,
    };

    public ReadOnlySpan<RegisterName> Src => this._regs;
    public ReadOnlySpan<RegisterName> Dest => [];
    public int SourceLine { get; } = sourceLine;

    public static IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine) {
        return operands is [GprId t, Memory m] ? new StoreOp<TOp>(t, m, sourceLine) : null;
    }

    public Effect Eval(ReadOnlySpan<uint> src, Span<uint> dest) {
        Address addr = new(unchecked(src[1] + this._offset));
        return (addr.Addr & AlignMask) != 0
            ? new Effect(new AddressError(addr, Store: true))
            : new Effect(new Store(addr, TOp.Kind, src[0]));
    }
}
