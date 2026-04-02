namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// アドレスを表す値型
/// </summary>
/// <param name="Addr">アドレスとなる<see langword="int"/></param>
internal record struct Address(uint Addr) {
    public static Address FromInstructionIndex(InstructionIndex iIdx, bool isKernelMode) {
        return isKernelMode
            ? new Address((uint)iIdx.Idx * 4) + TextSegment.KernelTextSegmentBase
            : new Address((uint)iIdx.Idx * 4) + TextSegment.TextSegmentBase;
    }

    public static Address FromInstructionIndex(InstructionIndex iIdx, Address offset) {
        return new Address((uint)iIdx.Idx * 4) + offset;

    }

    public static Address operator +(Address lhs, Address rhs) {
        lhs.Addr += rhs.Addr;
        return lhs;
    }

    public static Address operator +(Address lhs, int rhs) {
        return new Address(unchecked(lhs.Addr + (uint)rhs));
    }

    public static Address operator +(Address lhs, uint rhs) {
        lhs.Addr += rhs;
        return lhs;
    }

    public static Address operator ++(Address val) {
        val.Addr++;
        return val;
    }

    public static Address operator -(Address lhs, Address rhs) {
        checked {
            lhs.Addr -= rhs.Addr;
        }
        return lhs;
    }

    public static Address operator -(Address lhs, uint rhs) {
        checked {
            lhs.Addr -= rhs;
        }
        return lhs;
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

    public static readonly Address InValid = new(0);
}
