using PlusPim.Instructions;
using static PlusPim.Asm.Tests.Harness;
using static PlusPim.Instructions.GprId;

namespace PlusPim.Asm.Tests;

/// <summary>
/// ニーモニックの代表値と境界値 (docs/tests/assembler/values.md)
/// </summary>
/// <remarks>行の直前に置くラベル <c>label</c> はテキストの先頭を指す</remarks>
public class MnemonicTests {
    private static readonly Memory Sp4 = new(Sp, new Immediate(4));

    /// <summary>実命令の代表値と, 一覧表から直接作った期待値</summary>
    private static readonly Dictionary<string, (string Mnemonic, Operand[] Operands)> Representatives = Build();

    private static Dictionary<string, (string, Operand[])> Build() {
        Dictionary<string, (string, Operand[])> d = [];
        void Add(IEnumerable<string> mnemonics, string operands, params Operand[] ops) {
            foreach(string m in mnemonics) {
                d.Add($"{m} {operands}".TrimEnd(), (m, ops));
            }
        }
        Add(["add", "addu", "sub", "subu", "and", "or", "xor", "nor", "slt", "sltu"], "$t0, $t1, $t2", T0, T1, T2);
        Add(["sllv", "srlv", "srav"], "$t0, $t1, $t2", T0, T1, T2);
        Add(["addi", "addiu", "slti", "sltiu"], "$t0, $t1, -1", T0, T1, Imm(-1));
        Add(["andi", "ori", "xori"], "$t0, $t1, 0xffff", T0, T1, Imm(0xffff));
        Add(["sll", "srl", "sra"], "$t0, $t1, 4", T0, T1, Imm(4));
        Add(["lui"], "$t0, 0x1000", T0, Imm(0x1000));
        Add(["beq", "bne"], "$t0, $t1, label", T0, T1, T);
        Add(["bgez", "bgtz", "blez", "bltz", "bgezal", "bltzal"], "$t0, label", T0, T);
        Add(["j", "jal"], "label", T);
        Add(["jr"], "$ra", Ra);
        Add(["jalr"], "$t0", T0);
        Add(["jalr"], "$t1, $t0", T1, T0);
        Add(["mult", "multu", "div", "divu"], "$t0, $t1", T0, T1);
        Add(["mfhi", "mflo", "mthi", "mtlo"], "$t0", T0);
        Add(["lb", "lbu", "lh", "lhu", "lw", "lwl", "lwr"], "$t0, 4($sp)", T0, Sp4);
        Add(["sb", "sh", "sw", "swl", "swr"], "$t0, 4($sp)", T0, Sp4);
        Add(["mfc0", "mtc0"], "$t0, $12", T0, (GprId)12);
        Add(["syscall", "break", "eret", "runtime_call!"], "");
        Add(["teq", "tne", "tge", "tgeu", "tlt", "tltu"], "$t0, $t1", T0, T1);
        Add(["teqi", "tnei", "tgei", "tgeiu", "tlti", "tltiu"], "$t0, -1", T0, Imm(-1));
        return d;
    }

    public static TheoryData<string> Lines() {
        return [.. Representatives.Keys];
    }

    [Fact]
    public void Representatives_CoverEveryRealInstruction() {
        Assert.Equal(InstructionSet.ByMnemonic.Keys.Order(), Representatives.Values.Select(v => v.Mnemonic).Distinct().Order());
    }

    [Theory]
    [MemberData(nameof(Lines))]
    public void Representative_MatchesInstructionSet(string line) {
        AssemblyResult r = Assemble(Head + "label: " + line);
        r.AssertCodes();
        (string m, Operand[] ops) = Representatives[line];
        AssertSame(I(m, ops), r.Fetch(0));
    }

    /// <summary>表記は元の行と同じ. ラベルは絶対アドレスに, 省略した <c>$ra</c> は明示に変わる</summary>
    [Theory]
    [MemberData(nameof(Lines))]
    public void Representative_TextIsSourceLine(string line) {
        string expected = line == "jalr $t0" ? "jalr $ra, $t0" : line.Replace("label", T.ToString());
        Assert.Equal(expected, Assemble(Head + "label: " + line).Fetch(0).Text);
    }

    [Theory]
    [InlineData("addi $t0, $t1, 32767")]
    [InlineData("addi $t0, $t1, -32768")]
    [InlineData("ori $t0, $t1, 0x0")]
    [InlineData("sra $t0, $t1, 31")]
    [InlineData("lui $t0, 0xffff")]
    [InlineData("lw $t0, -4($sp)")]
    [InlineData("sw $zero, 0($gp)")]
    public void Immediate_TextFollowsKind(string line) {
        Assert.Equal(line, Assemble(Head + line).Fetch(0).Text);
    }

    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("ADD $t0, $t1, $t2", "ADD")]
    [InlineData("runtime_call", "runtime_call")]
    [InlineData("add! $t0, $t1, $t2", "add!")]
    public void Unknown_LineIsDropped(string line, string mnemonic) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes(WarningCode.UnknownMnemonic);
        Assert.Equal(mnemonic, r.Diagnostics[0].Args.Single());
        Assert.Equal(4, r.Diagnostics[0].Location?.Line);
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    [Theory]
    [InlineData("add $t0, $t1")]
    [InlineData("add $t0, $t1, $t2, $t3")]
    [InlineData("syscall $t0")]
    [InlineData("add $t0, $t1, 1")]
    [InlineData("addi $t0, $t1, $t2")]
    [InlineData("j $t0")]
    [InlineData("lw $t0, $sp")]
    [InlineData("add $t0, $t1, \"s\"")]
    public void WrongOperands_LineIsDropped(string line) {
        AssemblyResult r = Assemble(Head + line);
        r.AssertCodes(WarningCode.OperandMismatch);
        Assert.Equal(line.Split(' ')[0], r.Diagnostics[0].Args.Single());
        Assert.Null(r.Prog().Image.Fetch(T));
    }

    [Theory]
    [InlineData("$8")]
    [InlineData("$12")]
    [InlineData("$13")]
    [InlineData("$14")]
    public void Cp0Register_Accepted(string reg) {
        foreach(string m in new[] { "mfc0", "mtc0" }) {
            AssemblyResult r = Assemble(Head + $"{m} $t0, {reg}");
            r.AssertCodes();
            Assert.Equal($"{m} $t0, {reg}", r.Fetch(0).Text);
        }
    }

    [Theory]
    [InlineData("$0")]
    [InlineData("$31")]
    [InlineData("$t4")]
    public void Cp0Register_Rejected(string reg) {
        foreach(string m in new[] { "mfc0", "mtc0" }) {
            Assemble(Head + $"{m} $t0, {reg}").AssertCodes(WarningCode.OperandMismatch);
        }
    }

    [Fact]
    public void InstructionInData_IsDropped() {
        AssemblyResult r = Assemble(Head + "nop\n.data\nnop\n.byte 1");
        r.AssertCodes(WarningCode.MisplacedInstruction);
        Assert.Equal(6, r.Diagnostics[0].Location?.Line);
        Assert.Equal("01", r.DataAt(D));
    }
}
