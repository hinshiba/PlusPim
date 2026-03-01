namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// アドレスを表す値型
/// </summary>
/// <param name="Addr">アドレスとなる<see langword="int"/></param>
internal record struct Address(int Addr) {
    public static Address FromInstructionIndex(InstructionIndex IIdx) {
        return new Address((IIdx.Idx * 4) + TextSegment.TextSegmentBase.Addr);
    }

    public static Address operator +(Address lhs, Address rhs) {
        lhs.Addr += rhs.Addr;
        return lhs;
    }

    public static Address operator ++(Address val) {
        val.Addr++;
        return val;
    }

    public static int operator %(Address lhs, int rhs) {
        return lhs.Addr % rhs;
    }

    public override string ToString() {
        return $"Addr: (0x{this.Addr:X})";
    }
}
