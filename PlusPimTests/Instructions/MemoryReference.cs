using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// メモリアクセス命令の期待値を計算する，バイト単位のリトルエンディアン参照モデル (doc/tests/instructions/mem_model.md)
/// </summary>
/// <remarks>
/// 本体の実装 (<c>RuntimeContext.ReadMemoryBytes</c> 等) や <c>BinaryPrimitives</c> を使わず，1バイトずつの読み書きだけで定義する．
/// レジスタの「レーン i」は bit <c>8i</c>〜<c>8i+7</c> を指す (レーン 0 が最下位バイト)．
/// リトルエンディアンなので，アドレス <c>a + i</c> のバイトがワード <c>a</c> のレーン i に対応する．
/// 記録したメモリ窓の外へのアクセスは期待値の計算ミスとしてテストを失敗させる
/// </remarks>
internal sealed class MemoryReference {
    // ---- テストで使うメモリ配置 ----

    /// <summary>
    /// 実効アドレスの下位2bitを除いた部分 (アクセス対象のアラインされたワード)．実効アドレスは <c>TargetWord + {0,1,2,3}</c>
    /// </summary>
    public const uint TargetWord = 0x10010000;

    /// <summary>
    /// 対象ワードの前後に確保する余白のバイト数
    /// </summary>
    public const int Margin = 8;

    /// <summary>
    /// 記録するメモリ窓の先頭 (対象ワード - 8)
    /// </summary>
    public const uint WindowBase = TargetWord - Margin;

    /// <summary>
    /// 記録するメモリ窓の大きさ (余白 + 対象ワード + 余白)
    /// </summary>
    public const int WindowSize = Margin + 4 + Margin;

    /// <summary>
    /// メモリやレジスタに置く値．バイト対称なビットパターンだけではエンディアンの誤りを検出できないため，
    /// バイトがすべて異なる代表値を加える
    /// </summary>
    public static readonly IReadOnlyList<uint> DataValues = [.. TestValues.BitPatterns.Concat([0x000cafe0u, 0x800babe0u]).Distinct()];

    /// <summary>
    /// メモリ窓を事前に埋める非ゼロのバイト (位置ごとに異なる)
    /// </summary>
    public static byte FillByte(int offsetInWindow) {
        return (byte)(0xc0 + offsetInWindow);
    }

    /// <summary>
    /// メモリ窓全体を <see cref="FillByte"/> で埋める
    /// </summary>
    public static void FillWindow(RuntimeContext context) {
        for(int i = 0; i < WindowSize; i++) {
            context.WriteMemoryByte(new Address(WindowBase + (uint)i), FillByte(i));
        }
    }

    /// <summary>
    /// <paramref name="address"/> からリトルエンディアンで <paramref name="value"/> の4バイトを書き込む (テストの入力設定用)
    /// </summary>
    public static void WriteWord(RuntimeContext context, uint address, uint value) {
        for(int lane = 0; lane < 4; lane++) {
            context.WriteMemoryByte(new Address(address + (uint)lane), Lane(value, lane));
        }
    }

    /// <summary>
    /// メモリ窓を含めて状態を記録する
    /// </summary>
    public static MachineState Capture(RuntimeContext context) {
        return MachineState.Capture(context, new Address(WindowBase), WindowSize);
    }

    // ---- アドレス計算 ----

    /// <summary>
    /// 16bit オフセットの符号拡張
    /// </summary>
    public static uint SignExtendOffset(ushort offset) {
        return (offset & 0x8000) != 0 ? (0xffff0000u | offset) : offset;
    }

    /// <summary>
    /// 実効アドレス = ベースレジスタ値 + 符号拡張した16bitオフセット (values.md)
    /// </summary>
    public static uint EffectiveAddress(uint baseValue, ushort offset) {
        return unchecked(baseValue + SignExtendOffset(offset));
    }

    /// <summary>
    /// 実効アドレスが <paramref name="effectiveAddress"/> になるベースレジスタ値
    /// </summary>
    public static uint BaseFor(uint effectiveAddress, ushort offset) {
        return unchecked(effectiveAddress - SignExtendOffset(offset));
    }

    /// <summary>
    /// <paramref name="address"/> が <paramref name="size"/> バイト境界にアラインされているか
    /// </summary>
    public static bool IsAligned(uint address, int size) {
        return (address % (uint)size) == 0;
    }

    // ---- レーン操作 ----

    /// <summary>
    /// <paramref name="value"/> のレーン <paramref name="lane"/> のバイト
    /// </summary>
    public static byte Lane(uint value, int lane) {
        return (byte)((value >> (8 * lane)) & 0xff);
    }

    /// <summary>
    /// <paramref name="value"/> のレーン <paramref name="lane"/> だけを <paramref name="b"/> に置き換えた値
    /// </summary>
    public static uint WithLane(uint value, int lane, byte b) {
        uint mask = 0xffu << (8 * lane);
        return (value & ~mask) | ((uint)b << (8 * lane));
    }

    // ---- メモリ窓 ----

    private readonly uint _base;
    private readonly byte[] _bytes;

    private MemoryReference(uint baseAddress, byte[] bytes) {
        this._base = baseAddress;
        this._bytes = bytes;
    }

    /// <summary>
    /// 状態のメモリ窓の複製から参照モデルを作る
    /// </summary>
    public static MemoryReference Of(MachineState state) {
        return new MemoryReference(state.MemoryBase.Addr, (byte[])state.Memory.Clone());
    }

    /// <summary>
    /// <paramref name="state"/> のメモリ窓をこの参照モデルの内容に置き換えた状態を返す
    /// </summary>
    public MachineState ApplyTo(MachineState state) {
        Assert.Equal(this._base, state.MemoryBase.Addr);
        Assert.Equal(this._bytes.Length, state.Memory.Length);
        return state with { Memory = (byte[])this._bytes.Clone() };
    }

    /// <summary>
    /// 1バイトの読み書き
    /// </summary>
    public byte this[uint address] {
        get => this._bytes[this.Index(address)];
        set => this._bytes[this.Index(address)] = value;
    }

    private int Index(uint address) {
        long index = (long)address - this._base;
        Assert.True(index >= 0 && index < this._bytes.Length, $"0x{address:x8} is outside the reference memory window.");
        return (int)index;
    }

    // ---- ロード ----

    /// <summary>
    /// <paramref name="address"/> から <paramref name="size"/> バイトを読み，32bit に拡張する
    /// </summary>
    /// <param name="signed"><see langword="true"/> なら符号拡張，<see langword="false"/> ならゼロ拡張</param>
    public uint Load(uint address, int size, bool signed) {
        uint value = 0;
        for(int lane = 0; lane < size; lane++) {
            value = WithLane(value, lane, this[address + (uint)lane]);
        }
        if(signed && size < 4 && (Lane(value, size - 1) & 0x80) != 0) {
            for(int lane = size; lane < 4; lane++) {
                value = WithLane(value, lane, 0xff);
            }
        }
        return value;
    }

    /// <summary>
    /// <c>lwl</c> (MIPS32，リトルエンディアン)．b = EA &amp; 3 として，
    /// レジスタの上位レーン 3, 2, ..., 3-b にアドレス EA, EA-1, ..., (EA &amp; ~3) のバイトを入れ，残りのレーンは保持する
    /// </summary>
    public uint LoadWordLeft(uint effectiveAddress, uint rt) {
        uint b = effectiveAddress & 3;
        uint result = rt;
        for(uint i = 0; i <= b; i++) {
            result = WithLane(result, 3 - (int)i, this[effectiveAddress - i]);
        }
        return result;
    }

    /// <summary>
    /// <c>lwr</c> (MIPS32，リトルエンディアン)．b = EA &amp; 3 として，
    /// レジスタの下位レーン 0, 1, ..., 3-b にアドレス EA, EA+1, ..., (EA &amp; ~3)+3 のバイトを入れ，残りのレーンは保持する
    /// </summary>
    public uint LoadWordRight(uint effectiveAddress, uint rt) {
        uint b = effectiveAddress & 3;
        uint result = rt;
        for(uint i = 0; i <= 3 - b; i++) {
            result = WithLane(result, (int)i, this[effectiveAddress + i]);
        }
        return result;
    }

    // ---- ストア ----

    /// <summary>
    /// <paramref name="value"/> の下位 <paramref name="size"/> バイトを <paramref name="address"/> から書き込む
    /// </summary>
    public void Store(uint address, int size, uint value) {
        for(int lane = 0; lane < size; lane++) {
            this[address + (uint)lane] = Lane(value, lane);
        }
    }

    /// <summary>
    /// <c>swl</c> (MIPS32，リトルエンディアン)．b = EA &amp; 3 として，
    /// アドレス EA, EA-1, ..., (EA &amp; ~3) にレジスタのレーン 3, 2, ..., 3-b を書き込む
    /// </summary>
    public void StoreWordLeft(uint effectiveAddress, uint rt) {
        uint b = effectiveAddress & 3;
        for(uint i = 0; i <= b; i++) {
            this[effectiveAddress - i] = Lane(rt, 3 - (int)i);
        }
    }

    /// <summary>
    /// <c>swr</c> (MIPS32，リトルエンディアン)．b = EA &amp; 3 として，
    /// アドレス EA, EA+1, ..., (EA &amp; ~3)+3 にレジスタのレーン 0, 1, ..., 3-b を書き込む
    /// </summary>
    public void StoreWordRight(uint effectiveAddress, uint rt) {
        uint b = effectiveAddress & 3;
        for(uint i = 0; i <= 3 - b; i++) {
            this[effectiveAddress + i] = Lane(rt, (int)i);
        }
    }
}
