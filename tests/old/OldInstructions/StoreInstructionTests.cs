using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// ストア命令 <c>sb</c>，<c>sh</c>，<c>sw</c>，<c>swl</c>，<c>swr</c> (<c>op $rt, offset($rs)</c>) の命令レベルテスト
/// (doc/tests/instructions/instruction_tests.md，mem_model.md)
/// </summary>
/// <remarks>
/// 実効アドレスが <see cref="MemoryReference.TargetWord"/> + {0,1,2,3} になるようにベースレジスタを選ぶ．
/// メモリ窓 (対象ワード ±8 バイト) は事前に非ゼロのパターンで埋め，対象バイト以外が変化しないことを確認する．
/// エイリアスパターンは <c>rt==rs</c> (<see cref="Alias2Reg.Same"/>) となし．
/// <c>rt==rs</c> では書き込む値はベースレジスタ値そのものなので，テストパラメータの <c>rtValue</c> にその値を入れる
/// </remarks>
public class StoreInstructionTests {
    public static readonly IReadOnlyList<string> Mnemonics = ["sb", "sh", "sw", "swl", "swr"];

    /// <summary>
    /// アライメント検査を行う命令のアクセスサイズ．swl/swr はアライメント例外を起こさないので <see langword="null"/>
    /// </summary>
    private static int? CheckedSize(string op) {
        return op switch {
            "sb" => 1,
            "sh" => 2,
            "sw" => 4,
            "swl" or "swr" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    private static bool IsMisaligned(string op, uint effectiveAddress) {
        return CheckedSize(op) is int size && !MemoryReference.IsAligned(effectiveAddress, size);
    }

    public static TheoryData<string, Alias2Reg, ushort, uint, uint> NormalCases() {
        return Cases(misaligned: false);
    }

    private static TheoryData<string, Alias2Reg, ushort, uint, uint> Cases(bool misaligned) {
        TheoryData<string, Alias2Reg, ushort, uint, uint> data = new();
        foreach(string op in Mnemonics) {
            foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
                foreach(ushort offset in TestValues.Signed16) {
                    foreach(uint lowBits in TestValues.AlignmentLowBits) {
                        uint ea = MemoryReference.TargetWord + lowBits;
                        if(IsMisaligned(op, ea) != misaligned) {
                            continue;
                        }
                        if(alias.IsAliased()) {
                            // rt==rs なので書き込む値はベースレジスタ値に決まる
                            data.Add(op, alias, offset, lowBits, MemoryReference.BaseFor(ea, offset));
                            continue;
                        }
                        foreach(uint rtValue in MemoryReference.DataValues) {
                            data.Add(op, alias, offset, lowBits, rtValue);
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
            return before.WithException(ExcCode.AdES, before.PC, badVAddr: ea);
        }
        MemoryReference memory = MemoryReference.Of(before);
        uint value = before.Registers[(int)rt];
        switch(op) {
            case "sb":
                memory.Store(ea, 1, value);
                break;
            case "sh":
                memory.Store(ea, 2, value);
                break;
            case "sw":
                memory.Store(ea, 4, value);
                break;
            case "swl":
                memory.StoreWordLeft(ea, value);
                break;
            case "swr":
                memory.StoreWordRight(ea, value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op));
        }
        // 対象バイトだけが変化し，レジスタも PC も変化しない
        return memory.ApplyTo(before);
    }

    private static IInstruction Parse(string op, RegisterID rt, RegisterID rs, ushort offset) {
        return InstructionHarness.Parse($"{op} {RegisterAliases.Name(rt)}, {InstructionHarness.Imm(offset)}({RegisterAliases.Name(rs)})");
    }

    /// <summary>
    /// メモリ窓を非ゼロのパターンで埋め，rt に <paramref name="rtValue"/> を，
    /// 実効アドレスが <c>TargetWord + lowBits</c> になるようにベースレジスタを設定する
    /// </summary>
    private static RuntimeContext Setup(RegisterID rt, RegisterID rs, ushort offset, uint lowBits, uint rtValue) {
        uint baseValue = MemoryReference.BaseFor(MemoryReference.TargetWord + lowBits, offset);
        if(rt == rs) {
            Assert.Equal(baseValue, rtValue);
        }
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.FillWindow(context);
        context.Registers[rt] = rtValue;
        context.Registers[rs] = baseValue;
        return context;
    }

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Execute_Normal_StoresToMemory(string op, Alias2Reg alias, ushort offset, uint lowBits, uint rtValue) {
        (RegisterID rt, RegisterID rs) = RegisterAliases.Registers(alias);
        IInstruction inst = Parse(op, rt, rs, offset);
        RuntimeContext context = Setup(rt, rs, offset, lowBits, rtValue);
        MachineState before = MemoryReference.Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState expected = Expected(before, op, rt, rs, offset);
        MachineState.AssertEqual(expected, MemoryReference.Capture(context));
        Assert.False(context.IsKernelMode);
    }

    /// <summary>
    /// 参照モデルの swl/swr が MIPS32 リトルエンディアンのマスクによる定義と一致する (参照モデル自体の検証)
    /// </summary>
    [Fact]
    public void Reference_SwlSwr_AgreeWithMaskDefinition() {
        foreach(uint word in MemoryReference.DataValues) {
            foreach(uint rt in MemoryReference.DataValues) {
                foreach(uint b in TestValues.AlignmentLowBits) {
                    uint ea = MemoryReference.TargetWord + b;
                    int shiftLeft = 8 * (3 - (int)b);
                    int shiftRight = 8 * (int)b;

                    uint swl = (word & ~(0xffffffffu >> shiftLeft)) | (rt >> shiftLeft);
                    uint swr = (word & ~(0xffffffffu << shiftRight)) | (rt << shiftRight);

                    MemoryReference left = MemoryReference.Of(WindowWith(word));
                    left.StoreWordLeft(ea, rt);
                    Assert.Equal(swl, left.Load(MemoryReference.TargetWord, 4, signed: false));

                    MemoryReference right = MemoryReference.Of(WindowWith(word));
                    right.StoreWordRight(ea, rt);
                    Assert.Equal(swr, right.Load(MemoryReference.TargetWord, 4, signed: false));
                }
            }
        }
    }

    /// <summary>
    /// 参照モデルで <c>swr EA</c>，<c>swl EA+3</c> の順に実行すると非アラインのワードがリトルエンディアンで書ける (参照モデル自体の検証)
    /// </summary>
    [Fact]
    public void Reference_SwrThenSwl_StoresUnalignedWord() {
        const uint value = 0x800babe0;
        foreach(uint b in TestValues.AlignmentLowBits) {
            uint ea = MemoryReference.TargetWord + b;
            MachineState before = WindowWith(0x000cafe0);
            MemoryReference memory = MemoryReference.Of(before);
            memory.StoreWordRight(ea, value);
            memory.StoreWordLeft(ea + 3, value);

            MachineState expected = before.WithMemory(new(ea), 0xe0, 0xab, 0x0b, 0x80);
            Assert.Equal(expected.Memory, memory.ApplyTo(before).Memory);
        }
    }

    private static MachineState WindowWith(uint word) {
        RuntimeContext context = InstructionHarness.CreateUser();
        MemoryReference.FillWindow(context);
        MemoryReference.WriteWord(context, MemoryReference.TargetWord, word);
        return MemoryReference.Capture(context);
    }

}
