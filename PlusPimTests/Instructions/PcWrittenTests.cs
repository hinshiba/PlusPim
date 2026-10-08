using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// 各命令が「命令自身が PC を設定したか」(<see cref="ExecuteResult.PcWritten"/>) を正しく報告することのテスト
/// (doc/tests/instructions/execution_model.md「共通」)
/// </summary>
/// <remarks>
/// PC を設定するのはブランチ (成立・不成立とも)，ジャンプ，<c>eret</c> のみ．
/// 例外・ランタイムエラーを起こした実行は PC を設定しない
/// </remarks>
public class PcWrittenTests {
    /// <summary>
    /// 等しい値を持つ2つのレジスタ ($t0，$t1) と，異なる値を持つレジスタ ($t2)，負の値を持つレジスタ ($t4)．
    /// $t0 はメモリアクセスの整列したベースアドレスを兼ねる
    /// </summary>
    private const uint BaseValue = 0x10000000;
    private const uint OtherValue = 0x10000004;
    private const uint NegativeValue = 0x80000000;

    /// <summary>
    /// 実行の結果．意図した経路を通ったことを確かめる
    /// </summary>
    public enum Outcome {
        Completed,
        Raised,
        Failed,
    }

    /// <summary>
    /// (アセンブリ行, カーネルモードで実行するか, 期待する PcWritten, 期待する実行結果)
    /// </summary>
    public static TheoryData<string, bool, bool, Outcome> Cases() {
        return new TheoryData<string, bool, bool, Outcome> {
            // R形式
            { "add $t3, $t0, $t2", false, false, Outcome.Completed },
            { "add $t3, $t4, $t4", false, false, Outcome.Raised }, // Ov
            { "addu $t3, $t0, $t2", false, false, Outcome.Completed },
            { "sub $t3, $t0, $t2", false, false, Outcome.Completed },
            { "sub $t3, $t4, $t0", false, false, Outcome.Raised }, // Ov
            { "subu $t3, $t0, $t2", false, false, Outcome.Completed },
            { "and $t3, $t0, $t2", false, false, Outcome.Completed },
            { "or $t3, $t0, $t2", false, false, Outcome.Completed },
            { "xor $t3, $t0, $t2", false, false, Outcome.Completed },
            { "nor $t3, $t0, $t2", false, false, Outcome.Completed },
            { "slt $t3, $t0, $t2", false, false, Outcome.Completed },
            { "sltu $t3, $t0, $t2", false, false, Outcome.Completed },
            { "sll $t3, $t0, 3", false, false, Outcome.Completed },
            { "srl $t3, $t0, 3", false, false, Outcome.Completed },
            { "sra $t3, $t0, 3", false, false, Outcome.Completed },
            { "sllv $t3, $t0, $t2", false, false, Outcome.Completed },
            { "srlv $t3, $t0, $t2", false, false, Outcome.Completed },
            { "srav $t3, $t0, $t2", false, false, Outcome.Completed },
            { "mult $t0, $t2", false, false, Outcome.Completed },
            { "multu $t0, $t2", false, false, Outcome.Completed },
            { "div $t0, $t2", false, false, Outcome.Completed },
            { "divu $t0, $t2", false, false, Outcome.Completed },
            { "div $t0, $zero", false, false, Outcome.Failed }, // DivisionByZero
            { "divu $t0, $zero", false, false, Outcome.Failed }, // DivisionByZero
            { "mfhi $t3", false, false, Outcome.Completed },
            { "mflo $t3", false, false, Outcome.Completed },
            { "mthi $t3", false, false, Outcome.Completed },
            { "mtlo $t3", false, false, Outcome.Completed },
            { "jr $t0", false, true, Outcome.Completed },
            { "jr $ra", false, true, Outcome.Completed }, // main からの戻り
            { "syscall", false, false, Outcome.Raised }, // Sys
            { "break", false, false, Outcome.Raised }, // Bp
            { "mfc0 $t3, $14", true, false, Outcome.Completed },
            { "mtc0 $t3, $14", true, false, Outcome.Completed },
            { "eret", true, true, Outcome.Completed },
            { "mfc0 $t3, $14", false, false, Outcome.Raised }, // CpU
            { "mtc0 $t3, $14", false, false, Outcome.Raised }, // CpU
            { "eret", false, false, Outcome.Raised }, // CpU
            { "mfc0 $t3, $0", true, false, Outcome.Failed }, // UnsupportedCP0Register
            { "mtc0 $t3, $0", true, false, Outcome.Failed }, // UnsupportedCP0Register
            // I形式
            { "addi $t3, $t0, 1", false, false, Outcome.Completed },
            { "addi $t3, $t4, -1", false, false, Outcome.Raised }, // Ov
            { "addiu $t3, $t0, 1", false, false, Outcome.Completed },
            { "andi $t3, $t0, 1", false, false, Outcome.Completed },
            { "ori $t3, $t0, 1", false, false, Outcome.Completed },
            { "xori $t3, $t0, 1", false, false, Outcome.Completed },
            { "slti $t3, $t0, 1", false, false, Outcome.Completed },
            { "sltiu $t3, $t0, 1", false, false, Outcome.Completed },
            { "lui $t3, 1", false, false, Outcome.Completed },
            { "beq $t0, $t1, fwd", false, true, Outcome.Completed }, // 成立
            { "beq $t0, $t2, fwd", false, true, Outcome.Completed }, // 不成立
            { "bne $t0, $t2, fwd", false, true, Outcome.Completed }, // 成立
            { "bne $t0, $t1, fwd", false, true, Outcome.Completed }, // 不成立
            { "bgez $t0, fwd", false, true, Outcome.Completed }, // 成立
            { "bgez $t4, fwd", false, true, Outcome.Completed }, // 不成立
            { "bgtz $t0, fwd", false, true, Outcome.Completed }, // 成立
            { "bgtz $zero, fwd", false, true, Outcome.Completed }, // 不成立
            { "blez $zero, fwd", false, true, Outcome.Completed }, // 成立
            { "blez $t0, fwd", false, true, Outcome.Completed }, // 不成立
            { "bltz $t4, fwd", false, true, Outcome.Completed }, // 成立
            { "bltz $t0, fwd", false, true, Outcome.Completed }, // 不成立
            { "lb $t3, 0($t0)", false, false, Outcome.Completed },
            { "lbu $t3, 0($t0)", false, false, Outcome.Completed },
            { "lh $t3, 0($t0)", false, false, Outcome.Completed },
            { "lhu $t3, 0($t0)", false, false, Outcome.Completed },
            { "lw $t3, 0($t0)", false, false, Outcome.Completed },
            { "lwl $t3, 0($t0)", false, false, Outcome.Completed },
            { "lwr $t3, 0($t0)", false, false, Outcome.Completed },
            { "sb $t3, 0($t0)", false, false, Outcome.Completed },
            { "sh $t3, 0($t0)", false, false, Outcome.Completed },
            { "sw $t3, 0($t0)", false, false, Outcome.Completed },
            { "swl $t3, 0($t0)", false, false, Outcome.Completed },
            { "swr $t3, 0($t0)", false, false, Outcome.Completed },
            { "lw $t3, 1($t0)", false, false, Outcome.Raised }, // AdEL
            { "lh $t3, 1($t0)", false, false, Outcome.Raised }, // AdEL
            { "lhu $t3, 1($t0)", false, false, Outcome.Raised }, // AdEL
            { "sh $t3, 1($t0)", false, false, Outcome.Raised }, // AdES
            { "sw $t3, 1($t0)", false, false, Outcome.Raised }, // AdES
            // J形式
            { "j fwd", false, true, Outcome.Completed },
            { "jal fwd", false, true, Outcome.Completed },
            // ランタイム
            { "runtime_call!", true, false, Outcome.Completed }, // exit
            { "runtime_call!", false, false, Outcome.Raised }, // CpU
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Execute_ReportsPcWritten(string line, bool kernel, bool expectedPcWritten, Outcome expectedOutcome) {
        IInstruction inst = InstructionHarness.Parse(line);
        RuntimeContext context = CreateContext(kernel);
        context.Registers[RegisterID.V0] = 10; // runtime_call! は exit

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.Equal(expectedOutcome, OutcomeOf(record));
        Assert.Equal(expectedPcWritten, record.Result.PcWritten);
    }

    [Fact]
    public void Execute_UnknownRuntimeCall_ReportsPcNotWritten() {
        IInstruction inst = InstructionHarness.Parse("runtime_call!");
        RuntimeContext context = CreateContext(kernel: true);
        context.Registers[RegisterID.V0] = 0;

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.Equal(Outcome.Failed, OutcomeOf(record));
        Assert.False(record.Result.PcWritten);
    }

    private static RuntimeContext CreateContext(bool kernel) {
        RuntimeContext context = kernel
            ? InstructionHarness.CreateKernel(ExcCode.Sys, InstructionHarness.InstructionAddress)
            : InstructionHarness.CreateUser();
        context.Registers[RegisterID.T0] = BaseValue;
        context.Registers[RegisterID.T1] = BaseValue;
        context.Registers[RegisterID.T2] = OtherValue;
        context.Registers[RegisterID.T4] = NegativeValue;
        context.Registers[RegisterID.Ra] = BaseValue;
        return context;
    }

    private static Outcome OutcomeOf(ExecutionRecord record) {
        return record.Raised ? Outcome.Raised : record.Failed ? Outcome.Failed : Outcome.Completed;
    }
}
