using PlusPim.Instructions;
using System.Runtime.CompilerServices;

namespace PlusPim.Dbg;

/// <summary>
/// 汎用レジスタ32本と <see cref="RName"/> のレジスタ
/// </summary>
/// <remarks>
/// 値のコピーで状態を複製できるようにインライン配列にしている.
/// </remarks>
[InlineArray(Count)]
public struct Registers: IEquatable<Registers> {
    /// <summary>汎用レジスタ32本と, <see cref="RName"/> の7本</summary>
    private const int Count = 32 + 7;
    private const uint CauseMask = 0x1Fu << 2;
    private uint _e0;

    public uint this[RegisterName r] {
        readonly get => this[IndexOf(r)];
        internal set {
            int i = IndexOf(r);
            if(i != 0) {
                this[i] = i == IndexOf(RName.Status) ? value & Cp0.Exl
                    : i == IndexOf(RName.Cause) ? value & CauseMask
                    : value;
            }
        }
    }

    public Address Pc {
        readonly get => new(this[RName.Pc]);
        internal set => this[RName.Pc] = value.Addr;
    }

    private static int IndexOf(RegisterName r) {
        return r switch {
            GprId g => (int)g,
            RName n => 32 + (int)n,
        };
    }

    public readonly bool Equals(Registers other) {
        return ((ReadOnlySpan<uint>)this).SequenceEqual(other);
    }

    public override readonly bool Equals(object? obj) {
        return obj is Registers r && this.Equals(r);
    }

    public override readonly int GetHashCode() {
        HashCode h = new();
        h.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes((ReadOnlySpan<uint>)this));
        return h.ToHashCode();
    }

    public static bool operator ==(Registers left, Registers right) {
        return left.Equals(right);
    }

    public static bool operator !=(Registers left, Registers right) {
        return !(left == right);
    }
}
