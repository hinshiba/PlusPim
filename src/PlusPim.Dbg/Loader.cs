using PlusPim.Image;
using PlusPim.Instructions;

namespace PlusPim.Dbg;

/// <summary>
/// 読み込み直後の状態を作る
/// </summary>
/// <remarks>初期値は位置0の状態に含まれるので, 先頭まで戻っても残る</remarks>
internal static class Loader {
    /// <summary><c>$sp</c> の初期値. <c>0x80000000 - 4 - 4096</c></summary>
    public const uint StackPointer = 0x7FFFEFFC;

    /// <summary><c>$gp</c> の初期値. データセグメントの先頭 + <c>0x8000</c></summary>
    public const uint GlobalPointer = 0x10008000;

    /// <summary>
    /// <c>$ra</c> の初期値. ここへのジャンプでエントリから戻ったとみなして終了する
    /// </summary>
    /// <remarks>どのセグメントにも属さず, <c>jal</c> の戻り先にならない</remarks>
    public static readonly Address Sentinel = new(0xFFFFFFFC);

    public static State Initial(ProgramImage image) {
        Memory memory = new();
        foreach((Address b, ReadOnlyMemory<byte> bytes) in image.Data) {
            ReadOnlySpan<byte> span = bytes.Span;
            for(int i = 0; i < span.Length; i++) {
                memory.Write(b + (uint)i, span[i]);
            }
        }
        State s = new(memory);
        s.Core.Registers[GprId.Sp] = StackPointer;
        s.Core.Registers[GprId.Gp] = GlobalPointer;
        s.Core.Registers[GprId.Ra] = Sentinel.Addr;
        s.Core.Registers.Pc = image.Entry;
        return s;
    }
}
