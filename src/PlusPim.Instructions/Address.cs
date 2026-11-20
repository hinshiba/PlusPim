namespace PlusPim.Instructions;

/// <summary>
/// アドレスを表す値型
/// </summary>
/// <param name="Addr">アドレスとなる<see langword="uint"/></param>
public record struct Address(uint Addr) {
    public static Address operator +(Address lhs, int rhs) {
        return new Address(unchecked(lhs.Addr + (uint)rhs));
    }

    public static Address operator +(Address lhs, uint rhs) {
        return new Address(unchecked(lhs.Addr + rhs));
    }

    public static Address operator ++(Address val) {
        return new Address(unchecked(val.Addr + 1));
    }

    public static uint operator -(Address lhs, Address rhs) {
        return unchecked(lhs.Addr - rhs.Addr);
    }

    public static Address operator -(Address lhs, uint rhs) {
        return new Address(unchecked(lhs.Addr - rhs));
    }

    public static bool operator <(Address lhs, Address rhs) {
        return lhs.Addr < rhs.Addr;
    }

    public static bool operator >(Address lhs, Address rhs) {
        return lhs.Addr > rhs.Addr;
    }

    public static bool operator <=(Address lhs, Address rhs) {
        return lhs.Addr <= rhs.Addr;
    }

    public static bool operator >=(Address lhs, Address rhs) {
        return lhs.Addr >= rhs.Addr;
    }

    public static int operator %(Address lhs, int rhs) {
        return (int)(lhs.Addr % rhs);
    }

    public override string ToString() {
        return $"0x{this.Addr:X}";
    }
}
