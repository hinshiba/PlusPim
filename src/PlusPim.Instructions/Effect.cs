using System.Runtime.CompilerServices;

namespace PlusPim.Instructions;

/// <summary>PCを1命令文進める.</summary>
public readonly record struct Next;

/// <summary>例外を発生させる. レジスタへの書き込みを行っていないことを保証する.</summary>
public readonly record struct Raise(ExcCode Code);

/// <summary>
/// 命令がランタイムへ要求する副作用
/// </summary>
/// <remarks>TODO: 大きさを最適化する</remarks>    
[Union]
public readonly struct Effect: IUnion {
    private enum Kind: byte {
        Next,
        Raise,
    }
    private readonly Kind _kind;
    private readonly ExcCode _code;

    public Effect(Next _) { }
    public Effect(Raise r) { this._kind = Kind.Raise; this._code = r.Code; }

    public static bool HasValue => true;
    public bool TryGetValue(out Next v) { v = default; return this._kind == Kind.Next; }
    public bool TryGetValue(out Raise v) { v = new(this._code); return this._kind == Kind.Raise; }

    public object? Value => this._kind switch {
        Kind.Next => new Next(),
        Kind.Raise => new Raise(this._code),
        _ => null,
    };
}
