using System.Runtime.CompilerServices;

namespace PlusPim.Instructions;

/// <summary>PCを1命令文進める.</summary>
public readonly record struct Next;

/// <summary>例外を発生させる. レジスタへの書き込みを行っていないことを保証する.</summary>
public readonly record struct Raise(ExcCode Code);

/// <summary>アドレス例外を発生させる. レジスタへの書き込みを行っていないことを保証する.</summary>
/// <param name="BadVAddr">例外の原因となった実効アドレス</param>
/// <param name="Store">ストアによる例外か</param>
public readonly record struct AddressError(Address BadVAddr, bool Store) {
    public ExcCode Code => this.Store ? ExcCode.AdES : ExcCode.AdEL;
}

/// <summary>ランタイムエラーを発生させる. レジスタへの書き込みを行っていないことを保証する.</summary>
public readonly record struct Fail(RuntimeErrorKind Kind);

/// <summary>PCを<paramref name="Target"/>にする.</summary>
public readonly record struct Jump(Address Target);

/// <summary>PCを<paramref name="Target"/>にし, コールスタックに積む. </summary>
public readonly record struct Call(Address Target);

/// <summary>PCを<paramref name="Target"/>にし, 例外のフレームをコールスタックから取り出す.</summary>
public readonly record struct ExceptionReturn(Address Target);

/// <summary><paramref name="Addr"/>から<paramref name="Kind"/>に従ってDest[0]にロードする.</summary>
public readonly record struct Load(Address Addr, LoadKind Kind);

/// <summary><paramref name="Addr"/>へ<paramref name="Kind"/>に従って<paramref name="Value"/>を書き込む.</summary>
public readonly record struct Store(Address Addr, StoreKind Kind, uint Value);

/// <summary>ランタイムへ<paramref name="Kind"/>番の機能を要求する.</summary>
/// <param name="Kind">ランタイムコール番号</param>
/// <param name="A0">$a0 の値</param>
/// <param name="A1">$a1 の値</param>
/// <remarks><see cref="RuntimeRequestKind.ReadInt"/> と <see cref="RuntimeRequestKind.ReadChar"/> の結果は Dest[0] へ書き込む.</remarks>
public readonly record struct RuntimeRequest(RuntimeRequestKind Kind, uint A0, uint A1);

/// <summary>ロードの範囲とDest[0] への書き込む手法./summary>
public enum LoadKind: byte {
    /// <summary><c>lb</c>: 1バイトを符号拡張する</summary>
    B,
    /// <summary><c>lbu</c>: 1バイトをゼロ拡張する</summary>
    BU,
    /// <summary><c>lh</c>: 2バイトを符号拡張する</summary>
    H,
    /// <summary><c>lhu</c>: 2バイトをゼロ拡張する</summary>
    HU,
    /// <summary><c>lw</c>: 4バイト</summary>
    W,
    /// <summary><c>lwl</c>: <c>Addr &amp; ~3</c> から <c>Addr</c> までをrtの上位バイトへ合成する</summary>
    Left,
    /// <summary><c>lwr</c>: <c>Addr</c> から <c>Addr | 3</c> までをrtの下位バイトへ合成する</summary>
    Right,
}

/// <summary>ストアの範囲とどのバイトを書き込むか.</summary>
public enum StoreKind: byte {
    /// <summary><c>sb</c>: 下位1バイト</summary>
    B,
    /// <summary><c>sh</c>: 下位2バイト</summary>
    H,
    /// <summary><c>sw</c>: 4バイト</summary>
    W,
    /// <summary><c>swl</c>: 上位バイトを <c>Addr &amp; ~3</c> から <c>Addr</c> までへ書く</summary>
    Left,
    /// <summary><c>swr</c>: 下位バイトを <c>Addr</c> から <c>Addr | 3</c> までへ書く</summary>
    Right,
}

/// <summary><c>runtime_call!</c> の機能番号.</summary>
public enum RuntimeRequestKind: byte {
    /// <summary>$a0 を符号付き10進数で出力する</summary>
    PrintInt = 1,
    /// <summary>$a0 が指す NUL 終端のバイト列を出力する</summary>
    PrintString = 4,
    /// <summary>1行を整数として読む</summary>
    ReadInt = 5,
    /// <summary>1行を $a0 から最大 $a1 - 1 バイトと NUL で書く</summary>
    ReadString = 8,
    /// <summary>実行を終了する</summary>
    Exit = 10,
    /// <summary>$a0 の下位1バイトを出力する</summary>
    PrintChar = 11,
    /// <summary>1バイトを読む. EOF では -1</summary>
    ReadChar = 12,
}

/// <summary>
/// 命令がランタイムへ要求する副作用
/// </summary>
/// <remarks>
/// 各ケースのペイロードを型の一致するフィールドで共有してサイズを削減している.
/// 現在は12バイト.
/// </remarks>
[Union]
public readonly struct Effect: IUnion {
    private enum Kind: byte {
        Next,
        Raise,
        AddressError,
        Fail,
        Jump,
        Call,
        ExceptionReturn,
        Load,
        Store,
        RuntimeRequest,
    }
    private readonly Kind _kind;
    /// <summary><see cref="ExcCode"/>, <see cref="RuntimeErrorKind"/>, <see cref="LoadKind"/>, <see cref="StoreKind"/>, <see cref="RuntimeRequestKind"/> のいずれか</summary>
    private readonly byte _small;
    private readonly Address _addr;
    private readonly uint _value;

    private Effect(Kind kind, byte small, Address addr, uint value = 0) {
        this._kind = kind;
        this._small = small;
        this._addr = addr;
        this._value = value;
    }

    public Effect(Next _) { }
    public Effect(Raise r) : this(Kind.Raise, (byte)r.Code, default) { }
    public Effect(AddressError e) : this(Kind.AddressError, (byte)e.Code, e.BadVAddr) { }
    public Effect(Fail f) : this(Kind.Fail, (byte)f.Kind, default) { }
    public Effect(Jump j) : this(Kind.Jump, 0, j.Target) { }
    public Effect(Call c) : this(Kind.Call, 0, c.Target) { }
    public Effect(ExceptionReturn e) : this(Kind.ExceptionReturn, 0, e.Target) { }
    public Effect(Load l) : this(Kind.Load, (byte)l.Kind, l.Addr) { }
    public Effect(Store s) : this(Kind.Store, (byte)s.Kind, s.Addr, s.Value) { }
    public Effect(RuntimeRequest r) : this(Kind.RuntimeRequest, (byte)r.Kind, new Address(r.A0), r.A1) { }

    private Raise AsRaise => new((ExcCode)this._small);
    private AddressError AsAddressError => new(this._addr, (ExcCode)this._small == ExcCode.AdES);
    private Fail AsFail => new((RuntimeErrorKind)this._small);
    private Jump AsJump => new(this._addr);
    private Call AsCall => new(this._addr);
    private ExceptionReturn AsExceptionReturn => new(this._addr);
    private Load AsLoad => new(this._addr, (LoadKind)this._small);
    private Store AsStore => new(this._addr, (StoreKind)this._small, this._value);
    private RuntimeRequest AsRuntimeRequest => new((RuntimeRequestKind)this._small, this._addr.Addr, this._value);

    public static bool HasValue => true;
    public bool TryGetValue(out Next v) { v = default; return this._kind == Kind.Next; }
    public bool TryGetValue(out Raise v) { v = this.AsRaise; return this._kind == Kind.Raise; }
    public bool TryGetValue(out AddressError v) { v = this.AsAddressError; return this._kind == Kind.AddressError; }
    public bool TryGetValue(out Fail v) { v = this.AsFail; return this._kind == Kind.Fail; }
    public bool TryGetValue(out Jump v) { v = this.AsJump; return this._kind == Kind.Jump; }
    public bool TryGetValue(out Call v) { v = this.AsCall; return this._kind == Kind.Call; }
    public bool TryGetValue(out ExceptionReturn v) { v = this.AsExceptionReturn; return this._kind == Kind.ExceptionReturn; }
    public bool TryGetValue(out Load v) { v = this.AsLoad; return this._kind == Kind.Load; }
    public bool TryGetValue(out Store v) { v = this.AsStore; return this._kind == Kind.Store; }
    public bool TryGetValue(out RuntimeRequest v) { v = this.AsRuntimeRequest; return this._kind == Kind.RuntimeRequest; }

    public object? Value => this._kind switch {
        Kind.Next => new Next(),
        Kind.Raise => this.AsRaise,
        Kind.AddressError => this.AsAddressError,
        Kind.Fail => this.AsFail,
        Kind.Jump => this.AsJump,
        Kind.Call => this.AsCall,
        Kind.ExceptionReturn => this.AsExceptionReturn,
        Kind.Load => this.AsLoad,
        Kind.Store => this.AsStore,
        Kind.RuntimeRequest => this.AsRuntimeRequest,
        _ => null,
    };
}
