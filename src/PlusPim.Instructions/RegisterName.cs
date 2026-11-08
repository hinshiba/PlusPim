using System.Runtime.CompilerServices;

namespace PlusPim.Instructions;

/// <summary>
/// 汎用レジスタ以外のレジスタを識別する列挙型
/// </summary>
public enum RName: byte {
    Pc,
    Hi,
    Lo,
    // CP0
    BadVAddr,
    Status,
    Cause,
    Epc,
}

/// <summary>
/// 命令が読み書きするレジスタの名前. <see cref="GprId"/> か <see cref="RName"/> のいずれか
/// </summary>
/// <remarks>既定値は <see cref="GprId.Zero"/></remarks>
[Union]
public readonly record struct RegisterName: IUnion {
    private readonly bool _isOther;
    private readonly byte _id;

    public RegisterName(GprId r) { this._id = (byte)r; }
    public RegisterName(RName r) { this._isOther = true; this._id = (byte)r; }

    public static bool HasValue => true;
    public bool TryGetValue(out GprId v) { v = (GprId)this._id; return !this._isOther; }
    public bool TryGetValue(out RName v) { v = (RName)this._id; return this._isOther; }

    public object? Value => this._isOther ? (RName)this._id : (object)(GprId)this._id;
}
