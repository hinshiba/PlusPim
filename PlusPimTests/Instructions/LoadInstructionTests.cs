using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// ロード命令 <c>lb</c>，<c>lbu</c>，<c>lh</c>，<c>lhu</c>，<c>lw</c>，<c>lwl</c>，<c>lwr</c> (<c>op $rt, offset($rs)</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，mem_model.md)
/// </summary>
/// <remarks>
/// 実効アドレスが <see cref="MemoryReference.TargetWord"/> + {0,1,2,3} になるようにベースレジスタを選ぶ．
/// テストパラメータの <c>word</c> はアラインされた対象ワードにリトルエンディアンで置く値である．
/// エイリアスパターンは <c>rt==rs</c> (<see cref="Alias2Reg.Same"/>) となし
/// </remarks>
public class LoadInstructionTests {
    /// <summary>
    /// エイリアスなしのときの rt の実行前の値 (lwl/lwr で保持されるレーンを区別できるよう，バイトがすべて異なる)
    /// </summary>
    private const uint PriorRt = 0x8badf00d;

    public static readonly IReadOnlyList<string> Mnemonics = ["lb", "lbu", "lh", "lhu", "lw", "lwl", "lwr"];

    /// <summary>
    /// アライメント検査を行う命令のアクセスサイズ．lwl/lwr はアライメント例外を起こさないので <see langword="null"/>
    /// </summary>
    private static int? CheckedSize(string op) {
        return op switch {
            "lb" or "lbu" => 1,
            "lh" or "lhu" => 2,
            "lw" => 4,
            "lwl" or "lwr" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static bool IsMisaligned(string op, uint effectiveAddress) {
        return CheckedSize(op) is int size && !MemoryReference.IsAligned(effectiveAddress, size);
    }

    public static TheoryData<string, Alias2Reg, ushort, uint, uint> NormalCases() {
        return Cases(misaligned: false);
    }

    public static TheoryData<string, Alias2Reg, ushort, uint, uint> MisalignedCases() {
        return Cases(misaligned: true);
    }

    private static TheoryData<string, Alias2Reg, ushort, uint, uint> Cases(bool misaligned) {
        TheoryData<string, Alias2Reg, ushort, uint, uint> data = new();
        foreach(string op in Mnemonics) {
            foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
                foreach(ushort offset in TestValues.Signed16) {
                    foreach(uint lowBits in TestValues.AlignmentLowBits) {
                        if(IsMisaligned(op, MemoryReference.TargetWord + lowBits) != misaligned) {
                            continue;
                        }
                        foreach(uint word in MemoryReference.DataValues) {
                            data.Add(op, alias, offset, lowBits, word);
                        }
                    }
                }
            }
        }
        return data;
    }

    /// <summary>
    /// 参照モデルによる期待値．実行前の状態だけから計算する
    /// </summary>
    private static MachineState Expected(MachineState before, string op, RegisterID rt, RegisterID rs, ushort offset) {
        uint ea = MemoryReference.EffectiveAddress(before.Registers[(int)rs], offset);
        if(IsMisaligned(op, ea)) {
            // レジスタもメモリも変化せず，CP0 だけが例外発生直後の状態になる
            return before.WithException(ExcCode.AdEL, before.PC, badVAddr: ea);
        }
        MemoryReference memory = MemoryReference.Of(before);
        uint prior = before.Registers[(int)rt];
        uint loaded = op switch {
            "lb" => memory.Load(ea, 1, signed: true),
            "lbu" => memory.Load(ea, 1, signed: false),
            "lh" => memory.Load(ea, 2, signed: true),
            "lhu" => memory.Load(ea, 2, signed: false),
            "lw" => memory.Load(ea, 4, signed: false),
            "lwl" => memory.LoadWordLeft(ea, prior),
            "lwr" => memory.LoadWordRight(ea, prior),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
        // rt だけが変化し，メモリも PC も変化しない
        return before.WithRegister(rt, loaded);
    }

    private static IInstruction Parse(string op, RegisterID rt, RegisterID rs, ushort offset) {
        return InstructionHarness.Parse($"{op} {RegisterAliases.Name(rt)}, {InstructionHarness.Imm(offset)}({RegisterAliases.Name(rs)})");
    }

    /// <summary>
    /// メモリ窓を非ゼロのパターンで埋め，対象ワードに <paramref name="word"/> を置き，
    /// 実効アドレスが <c>TargetWord + lowBits</c> になるようにベースレジスタを設定する
    /// </summary>
    private static RuntimeContext Setup(RegisterID rt, RegisterID rs, ushort offset, uint lowBits, uint word) {
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.FillWindow(context);
        MemoryReference.WriteWord(context, MemoryReference.TargetWord, word);
        context.Registers[rt] = PriorRt;
        // rt==rs のときはベースレジスタ値が rt の実行前の値になる
        context.Registers[rs] = MemoryReference.BaseFor(MemoryReference.TargetWord + lowBits, offset);
        return context;
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Execute_Normal_LoadsFromMemory(string op, Alias2Reg alias, ushort offset, uint lowBits, uint word) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, lowBits, word);
        MachineState before = MemoryReference.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = Expected(before, op, rt, rs, offset);
        MachineState.AssertEqual(expected, MemoryReference.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Undo_Normal_RestoresState(string op, Alias2Reg alias, ushort offset, uint lowBits, uint word) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, lowBits, word);
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MemoryReference.Capture(context));
    }

    [Theory]
    [MemberData(nameof(MisalignedCases))]
    public void Execute_Misaligned_RaisesAdEL(string op, Alias2Reg alias, ushort offset, uint lowBits, uint word) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, lowBits, word);
        MachineState before = MemoryReference.Capture(context);

        _ = Processor.Execute(context, inst);

        // BadVAddr = 実効アドレス，EPC = 命令アドレス．レジスタとメモリは変化しない
        MachineState expected = before.WithException(ExcCode.AdEL, InstructionHarness.InstructionAddress, badVAddr: MemoryReference.TargetWord + lowBits);
        MachineState.AssertEqual(expected, MemoryReference.Capture(context));
        Assert.True(context.IsKernelMode);
    }

    [Theory]
    [MemberData(nameof(MisalignedCases))]
    public void Undo_Misaligned_RestoresState(string op, Alias2Reg alias, ushort offset, uint lowBits, uint word) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, lowBits, word);
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, MemoryReference.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    [Fact]
    public void Parameters_CoverEveryInstructionAndAlignment() {
        TheoryData<string, Alias2Reg, ushort, uint, uint> normal = NormalCases();
        TheoryData<string, Alias2Reg, ushort, uint, uint> misaligned = MisalignedCases();
        Assert.NotEmpty(normal);
        Assert.NotEmpty(misaligned);

        HashSet<(string Op, uint LowBits)> normalKeys = [.. normal.Select(row => ((string)row[0], (uint)row[3]))];
        HashSet<(string Op, uint LowBits)> misalignedKeys = [.. misaligned.Select(row => ((string)row[0], (uint)row[3]))];
        foreach(string op in Mnemonics) {
            foreach(uint lowBits in TestValues.AlignmentLowBits) {
                // 各命令・各下位ビットパターンがちょうど一方の集合に現れる
                Assert.True(normalKeys.Contains((op, lowBits)) ^ misalignedKeys.Contains((op, lowBits)), $"{op} with low bits {lowBits}");
            }
        }
        // 異常系は lh/lhu/lw だけ
        string[] misalignedOps = ["lh", "lhu", "lw"];
        Assert.Equal(misalignedOps, misalignedKeys.Select(k => k.Op).Distinct().Order());
    }

    /// <summary>
    /// 参照モデルの lwl/lwr が MIPS32 リトルエンディアンのマスクによる定義と一致する (参照モデル自体の検証)
    /// </summary>
    [Fact]
    public void Reference_LwlLwr_AgreeWithMaskDefinition() {
        foreach(uint word in MemoryReference.DataValues) {
            foreach(uint rt in MemoryReference.DataValues.Append(PriorRt)) {
                foreach(uint b in TestValues.AlignmentLowBits) {
                    MemoryReference memory = MemoryReference.Of(WindowWith(word));
                    uint ea = MemoryReference.TargetWord + b;
                    int shiftLeft = 8 * (3 - (int)b);
                    int shiftRight = 8 * (int)b;

                    uint lwl = (word << shiftLeft) | (rt & (0x00ffffffu >> shiftRight));
                    uint lwr = (word >> shiftRight) | (rt & ~(0xffffffffu >> shiftRight));

                    Assert.Equal(lwl, memory.LoadWordLeft(ea, rt));
                    Assert.Equal(lwr, memory.LoadWordRight(ea, rt));
                }
            }
        }
    }

    /// <summary>
    /// 参照モデルで <c>lwr EA</c>，<c>lwl EA+3</c> の順に実行すると非アラインのワードがリトルエンディアンで読める (参照モデル自体の検証)
    /// </summary>
    [Fact]
    public void Reference_LwrThenLwl_LoadsUnalignedWord() {
        MachineState state = WindowWith(0x000cafe0);
        MemoryReference memory = MemoryReference.Of(state);
        foreach(uint b in TestValues.AlignmentLowBits) {
            uint ea = MemoryReference.TargetWord + b;
            uint expected = 0;
            for(int lane = 0; lane < 4; lane++) {
                expected |= (uint)state.MemoryByte(new(ea + (uint)lane)) << (8 * lane);
            }
            uint rt = memory.LoadWordRight(ea, PriorRt);
            rt = memory.LoadWordLeft(ea + 3, rt);
            Assert.Equal(expected, rt);
        }
    }

    private static MachineState WindowWith(uint word) {
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.FillWindow(context);
        MemoryReference.WriteWord(context, MemoryReference.TargetWord, word);
        return MemoryReference.Capture(context);
    }

    /// <summary>
    /// 同じ命令インスタンスで，下位ビットパターン・メモリの値・rt の値を変えながら3回実行し，逆順に undo する．
    /// アライメント例外を起こす命令では正常系と異常系を混ぜる (2回目が異常系)
    /// </summary>
    [Theory]
    [InlineData("lb", 0u, 3u, 1u)]
    [InlineData("lbu", 1u, 2u, 3u)]
    [InlineData("lh", 2u, 1u, 0u)]
    [InlineData("lhu", 0u, 3u, 2u)]
    [InlineData("lw", 0u, 2u, 0u)]
    [InlineData("lwl", 0u, 3u, 1u)]
    [InlineData("lwr", 1u, 2u, 3u)]
    public void RepeatedExecute_UndoesInReverseOrder(string op, uint lowBits1, uint lowBits2, uint lowBits3) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(Alias2Reg.None);
        // 代表的な正のオフセットを使う (負のオフセットは別のテストで確認する)
        const ushort offset = 0x0afe;
        // 1回目と3回目は正常系，2回目はアライメント検査を行う命令 (lh/lhu/lw) でだけ異常系
        Assert.False(IsMisaligned(op, MemoryReference.TargetWord + lowBits1));
        Assert.Equal(op is "lh" or "lhu" or "lw", IsMisaligned(op, MemoryReference.TargetWord + lowBits2));
        Assert.False(IsMisaligned(op, MemoryReference.TargetWord + lowBits3));
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, 0, 0);

        Action<RuntimeContext> Step(uint lowBits, uint word, uint rtValue) {
            return c => {
                MemoryReference.WriteWord(c, MemoryReference.TargetWord, word);
                c.Registers[rt] = rtValue;
                c.Registers[rs] = MemoryReference.BaseFor(MemoryReference.TargetWord + lowBits, offset);
            };
        }

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.RunWithMemory(
            inst, context, new(MemoryReference.WindowBase), MemoryReference.WindowSize,
            Step(lowBits1, 0x000cafe0, 0x8badf00d),
            Step(lowBits2, 0x800babe0, 0x12345678),
            Step(lowBits3, 0xff00ff00, 0xdeadbeef)
        );

        // 順方向の結果も参照モデルと一致する
        foreach((MachineState beforeExecute, MachineState afterExecute) in results) {
            MachineState.AssertEqual(Expected(beforeExecute, op, rt, rs, offset), afterExecute);
        }
    }

    /// <summary>
    /// 負のオフセット (0xFFFC = -4，0x8000 = -32768) は符号拡張され，実効アドレスは base + offset (ラップアラウンドあり)
    /// </summary>
    [Theory]
    [InlineData("lw", (ushort)0xFFFC)]
    [InlineData("lw", (ushort)0x8000)]
    public void Execute_NegativeOffset_UsesSignExtendedAddress(string op, ushort offset) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(Alias2Reg.None);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, 0, 0x12345678);
        MachineState before = MemoryReference.Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        Assert.Equal(MemoryReference.TargetWord, MemoryReference.EffectiveAddress(context.Registers[rs], offset));
        MachineState.AssertEqual(Expected(before, op, rt, rs, offset), MemoryReference.Capture(context));
        Assert.False(context.IsKernelMode);

        Processor.Undo(context, record);
        MachineState.AssertEqual(before, MemoryReference.Capture(context));
    }
}
