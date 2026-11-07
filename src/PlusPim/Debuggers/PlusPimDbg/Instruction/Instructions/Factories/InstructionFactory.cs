using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;

/// <summary>
/// 疑似命令の展開時に具象命令インスタンスを直接生成するためのファクトリ
/// </summary>
internal static class InstructionFactory {
    internal static IInstruction Ori(RegisterID rt, RegisterID rs, Immediate imm, int lineNumber) {
        return new ITypeInstruction(rt, rs, imm, lineNumber, "ori", (rsVal, immVal) => rsVal | immVal.ToUInt());
    }

    internal static IInstruction Lui(RegisterID rt, Immediate imm, int lineNumber) {
        return new ITypeInstruction(rt, RegisterID.Zero, imm, lineNumber, "lui",
            (_, immVal) => unchecked(immVal.ToUInt() << 16), isRegImm: true);
    }

    internal static IInstruction Addu(RegisterID rd, RegisterID rs, RegisterID rt, int lineNumber) {
        return new RType3RegInstruction(rd, rs, rt, lineNumber, "addu", (rsVal, rtVal) => unchecked(rsVal + rtVal));
    }

    internal static IInstruction Sll(RegisterID rd, RegisterID rt, Immediate shamt, int lineNumber) {
        return new RTypeShiftImmInstruction(rd, rt, shamt, lineNumber, "sll", (rtVal, shamtVal) => rtVal << shamtVal);
    }
}
