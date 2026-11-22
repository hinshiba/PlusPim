using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Globalization;

namespace PlusPim.Dbg;

/// <summary>
/// <c>runtime_call!</c> の各機能
/// </summary>
/// <remarks>
/// 入力は <see cref="InputTape"/> で保持する.
/// </remarks>
internal static class RuntimeCalls {
    /// <summary>
    /// <paramref name="rq"/> の機能を実行する
    /// </summary>
    /// <param name="s">実行中の状態. 入力の読み取り位置を進める</param>
    /// <param name="rq">要求された機能と引数</param>
    /// <param name="dest">Dest[0] は <c>$v0</c>. read_int と read_char の結果を書く</param>
    /// <param name="input">デバッギへの入力</param>
    /// <param name="output">デバッギの出力の書き込み先</param>
    /// <returns>
    /// 入力が足りなければ状態を変えずに <see langword="false"/>.
    /// テキストのメモリに触れる場合はランタイムエラー.
    /// </returns>
    public static bool TryRun(State s, RuntimeRequest rq, Span<uint> dest, InputTape input, IBufferWriter<byte> output) {
        ReadOnlySpan<byte> pending = input.From(s.InputCursor);
        ReadOnlySpan<byte> line;
        switch(rq.Kind) {
            case RuntimeRequestKind.PrintInt:
                _ = unchecked((int)rq.A0).TryFormat(output.GetSpan(11), out int n, default, CultureInfo.InvariantCulture);
                output.Advance(n);
                break;

            case RuntimeRequestKind.PrintString:
                // 出力は戻せないので, NULまでを検査してから出力する
                Address start = new(rq.A0);
                Address end = start;
                while(!MemoryMap.IsText(end) && s.Memory.Read(end) is not 0) {
                    end++;
                }
                if(MemoryMap.IsText(end)) {
                    FailTextAccess(s);
                    return true;
                }
                int length = (int)(end - start);
                s.Memory.Read(start, output.GetSpan(length)[..length]);
                output.Advance(length);
                break;

            case RuntimeRequestKind.PrintChar:
                output.GetSpan(1)[0] = (byte)rq.A0;
                output.Advance(1);
                break;

            case RuntimeRequestKind.ReadInt:
                if(!TryLine(pending, input.Closed, out line)) {
                    return false;
                }
                // 前後の空白, 改行をを許す. 解釈できなければ0とし, 行を消費する.
                dest[0] = int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? unchecked((uint)v) : 0;
                s.InputCursor += line.Length;
                break;

            case RuntimeRequestKind.ReadChar:
                if(!pending.IsEmpty) {
                    dest[0] = pending[0];
                    s.InputCursor++;
                } else if(input.Closed) {
                    dest[0] = uint.MaxValue;
                } else {
                    return false;
                }
                break;

            case RuntimeRequestKind.ReadString:
                // 0なら何も書かず, 入力も読まない
                if(rq.A1 == 0) {
                    break;
                }
                if(!TryLine(pending, input.Closed, out line)) {
                    return false;
                }
                // 最大 $a1 - 1 バイトとNULを読み込む.
                int written = (int)Math.Min((uint)line.Length, rq.A1 - 1);
                Address at = new(rq.A0);
                for(uint i = 0; i <= (uint)written; i++) {
                    if(MemoryMap.IsText(at + i)) {
                        FailTextAccess(s);
                        return true;
                    }
                }
                for(int i = 0; i < written; i++) {
                    s.Memory.Write(at + (uint)i, line[i]);
                }
                s.Memory.Write(at + (uint)written, 0);
                s.InputCursor += written;
                break;

            case RuntimeRequestKind.Exit:
                s.Status = new Exited(ExitCause.ExitCall);
                break;
        }
        return true;
    }

    private static void FailTextAccess(State s) {
        s.Status = new RuntimeErrorRaised(RuntimeErrorKind.TextAccess, s.Registers.Pc);
    }

    /// <summary>
    /// 改行を含む1行の読み込みを試みる.
    /// </summary>
    private static bool TryLine(ReadOnlySpan<byte> pending, bool closed, out ReadOnlySpan<byte> line) {
        int nl = pending.IndexOf((byte)'\n');
        line = nl >= 0 ? pending[..(nl + 1)] : pending;
        return nl >= 0 || closed;
    }
}
