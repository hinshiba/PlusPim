using PlusPim.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Conformance.Tests;

/// <summary>
/// 命令語から作ったPlusPimの命令
/// </summary>
/// <param name="Instruction">実行する命令</param>
/// <param name="PcRelative">移り先が命令の番地から決まる. 番地を移すと移り先も同じだけ移る</param>
internal readonly record struct Decoded(IExecute Instruction, bool PcRelative);

/// <summary>
/// R3000の命令語をPlusPimの命令へ変換する
/// </summary>
/// <remarks>
/// PlusPimは命令語を持たないので, ニーモニックとオペランドを経て <see cref="InstructionSet"/> で作る.
/// R3000が無視するフィールドは無視する.
/// </remarks>
internal static class R3000Decoder {
    private enum Shape: byte {
        /// <summary><c>rd, rs, rt</c></summary>
        Rrr,
        /// <summary><c>rd, rt, shamt</c></summary>
        Shift,
        /// <summary><c>rd, rt, rs</c></summary>
        ShiftV,
        /// <summary><c>rs, rt</c></summary>
        HiLo,
        /// <summary><c>rd</c></summary>
        Rd,
        /// <summary><c>rs</c></summary>
        Rs,
        /// <summary><c>rd, rs</c></summary>
        Jalr,
        /// <summary>オペランドなし</summary>
        None,
        /// <summary><c>rt, rs, imm</c></summary>
        Imm,
        /// <summary><c>rt, imm</c></summary>
        Lui,
        /// <summary><c>rt, imm(rs)</c></summary>
        Mem,
        /// <summary><c>rs, rt, target</c></summary>
        Branch,
        /// <summary><c>rs, target</c></summary>
        BranchZ,
        /// <summary><c>target</c></summary>
        Jump,
    }

    /// <summary>SPECIALのfunctの表</summary>
    private static readonly FrozenDictionary<uint, (string, Shape)> Special = new Dictionary<uint, (string, Shape)> {
        [0x00] = ("sll", Shape.Shift),
        [0x02] = ("srl", Shape.Shift),
        [0x03] = ("sra", Shape.Shift),
        [0x04] = ("sllv", Shape.ShiftV),
        [0x06] = ("srlv", Shape.ShiftV),
        [0x07] = ("srav", Shape.ShiftV),
        [0x08] = ("jr", Shape.Rs),
        [0x09] = ("jalr", Shape.Jalr),
        [0x0C] = ("syscall", Shape.None),
        [0x0D] = ("break", Shape.None),
        [0x10] = ("mfhi", Shape.Rd),
        [0x11] = ("mthi", Shape.Rs),
        [0x12] = ("mflo", Shape.Rd),
        [0x13] = ("mtlo", Shape.Rs),
        [0x18] = ("mult", Shape.HiLo),
        [0x19] = ("multu", Shape.HiLo),
        [0x1A] = ("div", Shape.HiLo),
        [0x1B] = ("divu", Shape.HiLo),
        [0x20] = ("add", Shape.Rrr),
        [0x21] = ("addu", Shape.Rrr),
        [0x22] = ("sub", Shape.Rrr),
        [0x23] = ("subu", Shape.Rrr),
        [0x24] = ("and", Shape.Rrr),
        [0x25] = ("or", Shape.Rrr),
        [0x26] = ("xor", Shape.Rrr),
        [0x27] = ("nor", Shape.Rrr),
        [0x2A] = ("slt", Shape.Rrr),
        [0x2B] = ("sltu", Shape.Rrr),
    }.ToFrozenDictionary();

    /// <summary>opcodeの表. SPECIALとREGIMMを除く</summary>
    private static readonly FrozenDictionary<uint, (string, Shape)> Primary = new Dictionary<uint, (string, Shape)> {
        [0x02] = ("j", Shape.Jump),
        [0x03] = ("jal", Shape.Jump),
        [0x04] = ("beq", Shape.Branch),
        [0x05] = ("bne", Shape.Branch),
        [0x06] = ("blez", Shape.BranchZ),
        [0x07] = ("bgtz", Shape.BranchZ),
        [0x08] = ("addi", Shape.Imm),
        [0x09] = ("addiu", Shape.Imm),
        [0x0A] = ("slti", Shape.Imm),
        [0x0B] = ("sltiu", Shape.Imm),
        [0x0C] = ("andi", Shape.Imm),
        [0x0D] = ("ori", Shape.Imm),
        [0x0E] = ("xori", Shape.Imm),
        [0x0F] = ("lui", Shape.Lui),
        [0x20] = ("lb", Shape.Mem),
        [0x21] = ("lh", Shape.Mem),
        [0x22] = ("lwl", Shape.Mem),
        [0x23] = ("lw", Shape.Mem),
        [0x24] = ("lbu", Shape.Mem),
        [0x25] = ("lhu", Shape.Mem),
        [0x26] = ("lwr", Shape.Mem),
        [0x28] = ("sb", Shape.Mem),
        [0x29] = ("sh", Shape.Mem),
        [0x2A] = ("swl", Shape.Mem),
        [0x2B] = ("sw", Shape.Mem),
        [0x2E] = ("swr", Shape.Mem),
    }.ToFrozenDictionary();

    /// <summary>
    /// <paramref name="pc"/> に置いた <paramref name="word"/> を変換する. PlusPimにない命令なら <see langword="null"/>
    /// </summary>
    public static Decoded? Decode(uint word, Address pc) {
        uint op = word >> 26;
        GprId rs = (GprId)((word >> 21) & 31), rt = (GprId)((word >> 16) & 31), rd = (GprId)((word >> 11) & 31);
        Immediate imm = new((ushort)word);
        (string, Shape)? entry = op switch {
            0 => Special.TryGetValue(word & 0x3F, out (string, Shape) s) ? s : null,
            // R3000はrtのbit 0で条件を, bit 4..1が1000ならリンクを選び, 他のビットを無視する
            1 => ((((word >> 16) & 1) == 0 ? "bltz" : "bgez") + (((word >> 17) & 0xF) == 0x8 ? "al" : ""), Shape.BranchZ),
            _ => Primary.TryGetValue(op, out (string, Shape) p) ? p : null,
        };
        if(entry is not (string mnemonic, Shape shape)) {
            return null;
        }
        Address branch = pc + 4 + (imm.SignExtended << 2);
        Operand[] operands = shape switch {
            Shape.Rrr => [rd, rs, rt],
            Shape.Shift => [rd, rt, new Immediate((ushort)((word >> 6) & 31))],
            Shape.ShiftV => [rd, rt, rs],
            Shape.HiLo => [rs, rt],
            Shape.Rd => [rd],
            Shape.Rs => [rs],
            Shape.Jalr => [rd, rs],
            Shape.None => [],
            Shape.Imm => [rt, rs, imm],
            Shape.Lui => [rt, imm],
            Shape.Mem => [rt, new Memory(rs, imm)],
            Shape.Branch => [rs, rt, branch],
            Shape.BranchZ => [rs, branch],
            Shape.Jump => [new Address(((pc + 4).Addr & 0xF0000000) | ((word & 0x03FFFFFF) << 2))],
            _ => throw new System.Diagnostics.UnreachableException(),
        };
        return InstructionSet.ByMnemonic[mnemonic](operands) is IExecute inst
            ? new Decoded(inst, shape is Shape.Branch or Shape.BranchZ or Shape.Jump)
            : null;
    }
}
