using PlusPim.Instructions.Formats;
using PlusPim.Instructions.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Instructions;

/// <summary>
/// 実命令の一覧表
/// </summary>
/// <remarks>NativeAOTとトリミングのため, 静的に列挙する</remarks>
public static class InstructionSet {
    /// <summary>ニーモニックと対応する命令を作る関数を返す</summary>
    public static FrozenDictionary<string, FromOperands> ByMnemonic { get; } = new[] {
        Of<RBinOp<Add>>(), Of<RBinOp<Addu>>(), Of<RBinOp<Sub>>(), Of<RBinOp<Subu>>(),
        Of<RBinOp<And>>(), Of<RBinOp<Or>>(), Of<RBinOp<Xor>>(), Of<RBinOp<Nor>>(),
        Of<RBinOp<Slt>>(), Of<RBinOp<Sltu>>(), Of<RBinOp<Sllv>>(), Of<RBinOp<Srlv>>(), Of<RBinOp<Srav>>(),
        Of<ImmBinOp<Addi>>(), Of<ImmBinOp<Addiu>>(), Of<ImmBinOp<Andi>>(), Of<ImmBinOp<Ori>>(), Of<ImmBinOp<Xori>>(),
        Of<ImmBinOp<Slti>>(), Of<ImmBinOp<Sltiu>>(), Of<ImmBinOp<Sll>>(), Of<ImmBinOp<Srl>>(), Of<ImmBinOp<Sra>>(),
        Of<Lui>(),
        Of<BranchOp<Beq>>(), Of<BranchOp<Bne>>(),
        Of<BranchZOp<Bgez>>(), Of<BranchZOp<Bgtz>>(), Of<BranchZOp<Blez>>(), Of<BranchZOp<Bltz>>(),
        Of<BranchZLinkOp<Bgez>>(), Of<BranchZLinkOp<Bltz>>(),
        Of<J>(), Of<Jal>(), Of<Jr>(), Of<Jalr>(),
        Of<HiLoOp<Mult>>(), Of<HiLoOp<Multu>>(), Of<HiLoOp<Div>>(), Of<HiLoOp<Divu>>(),
        Of<MoveOp<Mfhi>>(), Of<MoveOp<Mflo>>(), Of<MoveOp<Mthi>>(), Of<MoveOp<Mtlo>>(),
        Of<LoadOp<Lb>>(), Of<LoadOp<Lbu>>(), Of<LoadOp<Lh>>(), Of<LoadOp<Lhu>>(), Of<LoadOp<Lw>>(),
        Of<LoadOp<Lwl>>(), Of<LoadOp<Lwr>>(),
        Of<StoreOp<Sb>>(), Of<StoreOp<Sh>>(), Of<StoreOp<Sw>>(), Of<StoreOp<Swl>>(), Of<StoreOp<Swr>>(),
        Of<Syscall>(), Of<Break>(),
        Of<TrapOp<Teq>>(), Of<TrapOp<Tne>>(), Of<TrapOp<Tge>>(), Of<TrapOp<Tgeu>>(), Of<TrapOp<Tlt>>(), Of<TrapOp<Tltu>>(),
        Of<TrapImmOp<Teq>>(), Of<TrapImmOp<Tne>>(), Of<TrapImmOp<Tge>>(), Of<TrapImmOp<Tgeu>>(), Of<TrapImmOp<Tlt>>(), Of<TrapImmOp<Tltu>>(),
        Of<Privileged<RuntimeCall>>(), Of<Privileged<Eret>>(), Of<Privileged<Mfc0>>(), Of<Privileged<Mtc0>>(),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static KeyValuePair<string, FromOperands> Of<T>() where T : IAssemblerable {
        return new(T.Mnemonic, T.FromOperands);
    }
}
