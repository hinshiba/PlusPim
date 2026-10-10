using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>mfhi $rd</c>，<c>mflo $rd</c>，<c>mthi $rs</c>，<c>mtlo $rs</c> の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md「Hi/Lo レジスタ操作」)
/// </summary>
/// <remarks>
/// 仕様の注記により，値は縮小した集合 (<see cref="TestValues.HiLo"/>) だけを用いる．
/// 1レジスタ命令なのでエイリアスパターンはエイリアスなしのみ
/// </remarks>
public class HiLoInstructionTests {
    private static readonly string[] Mnemonics = ["mfhi", "mflo", "mthi", "mtlo"];

    public static TheoryData<string, uint> AllCases() {
        TheoryData<string, uint> data = new();
        foreach(string mnemonic in Mnemonics) {
            foreach(uint value in TestValues.HiLo) {
                data.Add(mnemonic, value);
            }
        }
        return data;
    }

    private static IInstruction Parse(string mnemonic, RegisterID reg) {
        return InstructionHarness.Parse($"{mnemonic} {RegisterAliases.Name(reg)}");
    }

    /// <summary>
    /// 転送元に <paramref name="value"/>，転送先とその対になる HI/LO に異なる値を置いたコンテキストを作る
    /// </summary>
    /// <remarks>
    /// 転送先を <c>~value</c> にして書き込みの有無を，対になる HI/LO を別の値にして HI と LO の取り違えを検出する
    /// </remarks>
    private static RuntimeContext Setup(string mnemonic, RegisterID reg, uint value) {
        RuntimeContext context = InstructionHarness.CreateUser();
        uint other = value ^ 0x5a5a5a5a;
        switch(mnemonic) {
            case "mfhi":
                context.HI = value;
                context.LO = other;
                context.Registers[reg] = ~value;
                break;
            case "mflo":
                context.LO = value;
                context.HI = other;
                context.Registers[reg] = ~value;
                break;
            case "mthi":
                context.Registers[reg] = value;
                context.HI = ~value;
                context.LO = other;
                break;
            case "mtlo":
                context.Registers[reg] = value;
                context.LO = ~value;
                context.HI = other;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mnemonic));
        }
        return context;
    }

    /// <summary>
    /// 実装に依存しない期待値の計算
    /// </summary>
    private static MachineState Expected(string mnemonic, RegisterID reg, MachineState before) {
        return mnemonic switch {
            "mfhi" => before.WithRegister(reg, before.HI),
            "mflo" => before.WithRegister(reg, before.LO),
            "mthi" => before.WithHiLo(before.Registers[(int)reg], before.LO),
            "mtlo" => before.WithHiLo(before.HI, before.Registers[(int)reg]),
            _ => throw new ArgumentOutOfRangeException(nameof(mnemonic)),
        };
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Execute_Normal_TransfersValue(string mnemonic, uint value) {
        RegisterID reg = RegisterAliases.Single;
        IInstruction inst = Parse(mnemonic, reg);
        RuntimeContext context = Setup(mnemonic, reg, value);
        MachineState before = MachineState.Capture(context);

        _ = Processor.Execute(context, inst);

        // 転送先だけが変化する
        MachineState expected = Expected(mnemonic, reg, before);
        MachineState actual = MachineState.Capture(context);
        MachineState.AssertEqual(expected, actual);
        uint transferred = mnemonic switch {
            "mfhi" or "mflo" => actual.Registers[(int)reg],
            "mthi" => actual.HI,
            _ => actual.LO,
        };
        Assert.Equal(value, transferred);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Undo_Normal_RestoresState(string mnemonic, uint value) {
        RegisterID reg = RegisterAliases.Single;
        IInstruction inst = Parse(mnemonic, reg);
        RuntimeContext context = Setup(mnemonic, reg, value);
        MachineState before = MachineState.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MachineState.Capture(context));
    }

    [Fact]
    public void Parameters_CoverAllInstructions() {
        TheoryData<string, uint> data = AllCases();
        Assert.NotEmpty(TestValues.HiLo);
        foreach(string mnemonic in Mnemonics) {
            Assert.Contains(data, row => (string)row[0] == mnemonic);
        }
    }

    [Theory]
    [InlineData("mfhi")]
    [InlineData("mflo")]
    [InlineData("mthi")]
    [InlineData("mtlo")]
    public void RepeatedExecute_ThreeTimes_UndoesInReverseOrder(string mnemonic) {
        RegisterID reg = RegisterAliases.Single;
        IInstruction inst = Parse(mnemonic, reg);
        RuntimeContext context = InstructionHarness.CreateUser();

        // 転送元の値を変え，転送先も直前の結果とは異なる値にする
        Action<RuntimeContext>[] steps = [.. TestValues.HiLo.Select(value => (Action<RuntimeContext>)(c => {
            switch(mnemonic) {
                case "mfhi":
                    c.HI = value;
                    c.Registers[reg] = ~value;
                    break;
                case "mflo":
                    c.LO = value;
                    c.Registers[reg] = ~value;
                    break;
                case "mthi":
                    c.Registers[reg] = value;
                    c.HI = ~value;
                    break;
                default:
                    c.Registers[reg] = value;
                    c.LO = ~value;
                    break;
            }
        }))];
        Assert.Equal(3, steps.Length);

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.Run(inst, context, steps);

        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(Expected(mnemonic, reg, beforeExecute), afterExecute);
        }
    }
}
