namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// アドレスを表す値型
/// </summary>
/// <param name="Addr">アドレスとなる<see langword="int"/></param>
internal record struct Address(uint Addr) {
    public static Address FromInstructionIndex(InstructionIndex iIdx, Address baseAddr) {
        return new Address((uint)iIdx.Idx * 4) + baseAddr;
    }

    public static Address operator +(Address lhs, Address rhs) {
        lhs.Addr += rhs.Addr;
        return lhs;
    }

    public static Address operator +(Address lhs, int rhs) {
        lhs.Addr += (uint)rhs;
        return lhs;
    }

    public static Address operator ++(Address val) {
        val.Addr++;
        return val;
    }

    public static int operator %(Address lhs, int rhs) {
        return (int)(lhs.Addr % rhs);
    }

    public override string ToString() {
        return $"0x{this.Addr:X}";
    }
}
