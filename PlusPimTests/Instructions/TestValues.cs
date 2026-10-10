using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// テスト用パラメータ (doc/tests/instructions/values.md) と，その組の生成
/// </summary>
/// <remarks>
/// 16bit 値は即値のビットパターンそのものを <see langword="ushort"/> で表す．
/// 集合はいずれも重複のない読み取り専用リストである
/// </remarks>
public static class TestValues {
    // ---- 32bit 値 ----

    /// <summary>符号付き32bit値の代表値</summary>
    public static readonly IReadOnlyList<uint> Signed32Representative = [0x000cafe0, 0x800babe0, 0x00000000];

    /// <summary>符号付き32bit値の境界値</summary>
    public static readonly IReadOnlyList<uint> Signed32Boundary = [0x80000000, 0xffffffff, 0x00000000, 0x00000001, 0x7fffffff];

    /// <summary>符号付き32bit値の準境界値</summary>
    public static readonly IReadOnlyList<uint> Signed32NearBoundary = [0x7ffffffe, 0x80000001];

    /// <summary>符号付き32bit値 (代表値 ∪ 境界値 ∪ 準境界値)</summary>
    public static readonly IReadOnlyList<uint> Signed32 = Union(Signed32Representative, Signed32Boundary, Signed32NearBoundary);

    /// <summary>符号なし32bit値 (符号付き32bit値 ∪ {0xfffffffe})</summary>
    public static readonly IReadOnlyList<uint> Unsigned32 = Union<uint>(Signed32, [0xfffffffe]);

    /// <summary>ビットパターン</summary>
    public static readonly IReadOnlyList<uint> BitPatterns = [0x00000000, 0xffffffff, 0xaaaaaaaa, 0x55555555, 0xff00ff00, 0x00ff00ff];

    // ---- 16bit 値 ----

    /// <summary>符号付き16bit値の代表値</summary>
    public static readonly IReadOnlyList<ushort> Signed16Representative = [0x0afe, 0xbabe, 0x0000];

    /// <summary>符号付き16bit値の境界値</summary>
    public static readonly IReadOnlyList<ushort> Signed16Boundary = [0x8000, 0xffff, 0x0000, 0x0001, 0x7fff];

    /// <summary>符号付き16bit値の準境界値</summary>
    public static readonly IReadOnlyList<ushort> Signed16NearBoundary = [0x7ffe, 0x8001];

    /// <summary>符号付き16bit値 (代表値 ∪ 境界値 ∪ 準境界値)</summary>
    public static readonly IReadOnlyList<ushort> Signed16 = Union(Signed16Representative, Signed16Boundary, Signed16NearBoundary);

    /// <summary>符号なし16bit値 (符号付き16bit値 ∪ {0xfffe})</summary>
    public static readonly IReadOnlyList<ushort> Unsigned16 = Union<ushort>(Signed16, [0xfffe]);

    // ---- シフト量 ----

    /// <summary>即値のシフト量</summary>
    public static readonly IReadOnlyList<uint> ShiftAmountsImmediate = [0, 1, 15, 16, 31];

    /// <summary>レジスタのシフト量 (即値のシフト量 ∪ {32, 0xff, 0xffffffff})</summary>
    public static readonly IReadOnlyList<uint> ShiftAmountsRegister = Union<uint>(ShiftAmountsImmediate, [32, 0xff, 0xffffffff]);

    // ---- メモリアドレス ----

    /// <summary>実効アドレスの下位2bitのパターン</summary>
    public static readonly IReadOnlyList<uint> AlignmentLowBits = [0b00, 0b01, 0b10, 0b11];

    // ---- 縮小した集合 (instruction_tests.md で「すべて用いない」とされた命令) ----

    /// <summary>mfhi/mflo/mthi/mtlo 用の縮小集合 (符号付き32bit値の代表値)</summary>
    public static readonly IReadOnlyList<uint> HiLo = Signed32Representative;

    /// <summary>beq/bne 用の縮小集合 (符号付き32bit値の代表値)</summary>
    public static readonly IReadOnlyList<uint> BranchEquality = Signed32Representative;

    // ---- 命令ごとの割り当て ----

    /// <summary>add/sub/slt/mult/div のソース</summary>
    public static readonly IReadOnlyList<uint> SignedArithmetic = Signed32;

    /// <summary>addu/subu/sltu/multu/divu のソース</summary>
    public static readonly IReadOnlyList<uint> UnsignedArithmetic = Unsigned32;

    /// <summary>and/or/xor/nor のソース</summary>
    public static readonly IReadOnlyList<uint> Logical = BitPatterns;

    /// <summary>シフト命令の rt (ビットパターン ∪ 符号付き32bit値)</summary>
    public static readonly IReadOnlyList<uint> ShiftSources = Union(BitPatterns, Signed32);

    /// <summary>addi/addiu/slti の rs</summary>
    public static readonly IReadOnlyList<uint> ArithmeticImmediateRs = Signed32;

    /// <summary>addi/addiu/slti の即値</summary>
    public static readonly IReadOnlyList<ushort> ArithmeticImmediateImm = Signed16;

    /// <summary>sltiu の rs</summary>
    public static readonly IReadOnlyList<uint> SltiuRs = Unsigned32;

    /// <summary>sltiu の即値</summary>
    public static readonly IReadOnlyList<ushort> SltiuImm = Unsigned16;

    /// <summary>andi/ori/xori の rs</summary>
    public static readonly IReadOnlyList<uint> LogicalImmediateRs = BitPatterns;

    /// <summary>andi/ori/xori の即値</summary>
    public static readonly IReadOnlyList<ushort> LogicalImmediateImm = Unsigned16;

    /// <summary>lui の即値</summary>
    public static readonly IReadOnlyList<ushort> LuiImm = Unsigned16;

    /// <summary>bgez/bgtz/blez/bltz の rs</summary>
    public static readonly IReadOnlyList<uint> BranchZero = Signed32;

    // ---- 変換 ----

    /// <summary>
    /// 16bit 値を32bitへ符号拡張する
    /// </summary>
    public static uint SignExtend16(ushort value) {
        return unchecked((uint)(short)value);
    }

    // ---- 組の生成 ----

    /// <summary>
    /// 2つのソースの値の組．<paramref name="aliased"/> なら同じレジスタなので <c>(v, v)</c> (v ∈ <paramref name="first"/>) だけを返す
    /// </summary>
    public static IEnumerable<(uint First, uint Second)> Pairs(IReadOnlyList<uint> first, IReadOnlyList<uint> second, bool aliased) {
        foreach(uint a in first) {
            if(aliased) {
                yield return (a, a);
                continue;
            }
            foreach(uint b in second) {
                yield return (a, b);
            }
        }
    }

    /// <summary>
    /// 1つの値の集合のケース (1レジスタ命令など)
    /// </summary>
    public static TheoryData<uint> Cases(IReadOnlyList<uint> values, Func<uint, bool>? filter = null) {
        TheoryData<uint> data = new();
        foreach(uint v in values) {
            if(filter is null || filter(v)) {
                data.Add(v);
            }
        }
        return data;
    }

    /// <summary>
    /// 3レジスタ命令 <c>op r1, r2, r3</c> (r2, r3 がソース) の全エイリアスパターンと値の組．
    /// r2==r3 のパターンでは値を <paramref name="r2Values"/> から1つだけ選ぶ
    /// </summary>
    /// <param name="filter">(r2 の値, r3 の値) を受け取り，ケースに含めるなら <see langword="true"/></param>
    public static TheoryData<Alias3Reg, uint, uint> ThreeRegCases(IReadOnlyList<uint> r2Values, IReadOnlyList<uint> r3Values, Func<uint, uint, bool>? filter = null) {
        TheoryData<Alias3Reg, uint, uint> data = new();
        foreach(Alias3Reg alias in Enum.GetValues<Alias3Reg>()) {
            foreach((uint a, uint b) in Pairs(r2Values, r3Values, alias.SourcesAliased())) {
                if(filter is null || filter(a, b)) {
                    data.Add(alias, a, b);
                }
            }
        }
        return data;
    }

    /// <summary>
    /// <see cref="ThreeRegCases(IReadOnlyList{uint}, IReadOnlyList{uint}, Func{uint, uint, bool}?)"/> の両ソース同一集合版
    /// </summary>
    public static TheoryData<Alias3Reg, uint, uint> ThreeRegCases(IReadOnlyList<uint> values, Func<uint, uint, bool>? filter = null) {
        return ThreeRegCases(values, values, filter);
    }

    /// <summary>
    /// 2レジスタがともにソースの命令 (<c>mult $rs, $rt</c>，<c>beq $rs, $rt, label</c> など) の全エイリアスパターンと値の組．
    /// <see cref="Alias2Reg.Same"/> では値を <paramref name="r1Values"/> から1つだけ選ぶ
    /// </summary>
    public static TheoryData<Alias2Reg, uint, uint> TwoSourceCases(IReadOnlyList<uint> r1Values, IReadOnlyList<uint> r2Values, Func<uint, uint, bool>? filter = null) {
        TheoryData<Alias2Reg, uint, uint> data = new();
        foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
            foreach((uint a, uint b) in Pairs(r1Values, r2Values, alias.IsAliased())) {
                if(filter is null || filter(a, b)) {
                    data.Add(alias, a, b);
                }
            }
        }
        return data;
    }

    /// <summary>
    /// <see cref="TwoSourceCases(IReadOnlyList{uint}, IReadOnlyList{uint}, Func{uint, uint, bool}?)"/> の両ソース同一集合版
    /// </summary>
    public static TheoryData<Alias2Reg, uint, uint> TwoSourceCases(IReadOnlyList<uint> values, Func<uint, uint, bool>? filter = null) {
        return TwoSourceCases(values, values, filter);
    }

    /// <summary>
    /// 書き込み先 r1 とソース r2 の2レジスタに，レジスタでないオペランド (即値・シフト量) を伴う命令
    /// (<c>addi $rt, $rs, imm</c>，<c>sll $rd, $rt, shamt</c> など) の全エイリアスパターンと値の組．
    /// 値は直積をとる (エイリアスしてもソースは1つなので縮約しない)
    /// </summary>
    /// <typeparam name="T">即値なら <see langword="ushort"/>，シフト量なら <see langword="uint"/></typeparam>
    public static TheoryData<Alias2Reg, uint, T> DestSourceCases<T>(IReadOnlyList<uint> sourceValues, IReadOnlyList<T> operandValues, Func<uint, T, bool>? filter = null) {
        TheoryData<Alias2Reg, uint, T> data = new();
        foreach(Alias2Reg alias in Enum.GetValues<Alias2Reg>()) {
            foreach(uint s in sourceValues) {
                foreach(T o in operandValues) {
                    if(filter is null || filter(s, o)) {
                        data.Add(alias, s, o);
                    }
                }
            }
        }
        return data;
    }

    private static IReadOnlyList<T> Union<T>(params IReadOnlyList<T>[] sets) {
        return [.. sets.SelectMany(s => s).Distinct()];
    }
}
