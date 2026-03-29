using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;

/// <summary>
/// 疑似命令の展開時に具象命令インスタンスを直接生成するためのファクトリ
/// </summary>
internal static class InstructionFactory {
    internal static IInstruction Ori(RegisterID rt, RegisterID rs, Immediate imm, int lineIndex) {
        return new ITypeInstruction(rt, rs, imm, lineIndex, "ori", (rsVal, immVal) => rsVal | immVal.ToUInt());
    }

    internal static IInstruction Lui(RegisterID rt, Immediate imm, int lineIndex) {
        return new ITypeInstruction(rt, RegisterID.Zero, imm, lineIndex, "lui",
            (_, immVal) => unchecked(immVal.ToUInt() << 16));
    }

    internal static IInstruction Addu(RegisterID rd, RegisterID rs, RegisterID rt, int lineIndex) {
        return new RType3RegInstruction(rd, rs, rt, lineIndex, "addu", (rsVal, rtVal) => rsVal + rtVal);
    }

    internal static IInstruction Sll(RegisterID rd, RegisterID rt, Immediate shamt, int lineIndex) {
        return new RTypeShiftImmInstruction(rd, rt, shamt, lineIndex, "sll", (rtVal, shamtVal) => rtVal << shamtVal);
    }
}
