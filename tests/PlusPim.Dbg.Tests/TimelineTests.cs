using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Text;
using static PlusPim.Dbg.Tests.Harness;

namespace PlusPim.Dbg.Tests;

/// <summary>
/// 履歴のどの位置へどの経路で移っても, 履歴なしで同じステップ数だけ進めた状態と一致することを観測する
/// </summary>
/// <remarks>docs/tests/debugger/history_model.md</remarks>
public class TimelineTests {
    /// <summary>syscall を runtime_call! で処理して EPC + 4 へ戻るハンドラ</summary>
    private const string Handler = """
        .ktext
        handler:
            runtime_call!
            mfc0 $k0, $14
            addiu $k0, $k0, 4
            mtc0 $k0, $14
            eret
        """;

    /// <summary>呼び出し, 例外, メモリへの書き込み, 出力を繰り返し, チェックポイントの区間を何度もまたぐ</summary>
    /// <remarks>分岐とロードの遅延スロットにも命令を置く</remarks>
    private const string Loop = """
        .data
        buf: .word 0
        .text
        .globl main
        main:
            move $s7, $ra
            li $s0, 5000
        loop:
            move $a0, $s0
            jal store
            addiu $s0, $s0, -1
            andi $t0, $s0, 1023
            bne $t0, $zero, skip
            nop
            li $v0, 1
            syscall
        skip:
            bne $s0, $zero, loop
            nop
            jr $s7
            nop
        store:
            la $t1, buf
            lw $t2, 0($t1)
            sw $a0, 0($t1)
            jr $ra
            addu $v1, $v1, $t2
        """;

    /// <summary>EOF まで1バイトずつ読み, 合計を <c>$s0</c> に持つ</summary>
    private const string Echo = """
        .text
        .globl main
        main:
            move $s7, $ra
        loop:
            li $v0, 12
            syscall
            bltz $v0, done
            nop
            b loop
            addu $s0, $s0, $v0
        done:
            jr $s7
            nop
        """;

    private static readonly Address Buf = MemoryMap.Data.Base;
    private static readonly Probe<int> Any = (in Core c) => 0;
    private static readonly Probe<int> Never = (in Core c) => null;
    private static readonly Probe<int> Exit = (in Core c) => c.Status is Exited ? 0 : null;

    /// <summary><c>$s0</c> が <paramref name="value"/> の位置で止まる</summary>
    private static Probe<int> S0(uint value) {
        return (in Core c) => c.Registers[GprId.S0] == value ? 0 : null;
    }


    /// <summary>ある位置の観測値</summary>
    /// <param name="Core">メモリを除く状態</param>
    /// <param name="Buf">プログラムが書き込む唯一のワード</param>
    private readonly record struct Point(Core Core, uint Buf);

    private static Point Observe(StateView v) {
        Span<byte> b = stackalloc byte[4];
        v.ReadMemory(Buf, b);
        return new(v.Core, BitConverter.ToUInt32(b));
    }

    private static void AssertSame(Point expected, Point actual) {
        Assert.Equal(expected.Core.Registers, actual.Core.Registers);
        Assert.Equal(expected.Core.Status, actual.Core.Status);
        Assert.Equal(expected.Core.Depth, actual.Core.Depth);
        Assert.Equal(expected.Core.CallStack, actual.Core.CallStack);
        Assert.Equal(expected.Core.Branch, actual.Core.Branch);
        Assert.Equal(expected.Core.Load, actual.Core.Load);
        Assert.Equal(expected.Buf, actual.Buf);
    }

    /// <summary>履歴なしで終了まで進めた各位置の観測値と出力</summary>
    private sealed record Reference(List<Point> Points, byte[] Output) {
        public int Last => this.Points.Count - 1;

        public static Reference Of(ProgramImage image, string input = "") {
            Debuggee d = Debuggee.Load(image, DelaySlotsConfig.All);
            d.Feed(Encoding.ASCII.GetBytes(input));
            d.CloseInput();
            ArrayBufferWriter<byte> output = new();
            List<Point> points = [Observe(d.View)];
            while(d.View.Core.Status is Running or ExceptionRaised) {
                Assert.Equal(StepResult.Stepped, d.Step(output));
                points.Add(Observe(d.View));
            }
            Assert.True(d.View.Core.Status is Exited);
            return new(points, output.WrittenSpan.ToArray());
        }

        /// <summary>移った先が位置 <paramref name="position"/> の状態と一致する</summary>
        public void AssertAt(int position, Timeline t) {
            AssertSame(this.Points[position], Observe(t.View));
        }

        /// <summary><paramref name="before"/> より前で <paramref name="p"/> が値を返す最後の位置</summary>
        public int LastBefore(int before, Probe<int> p) {
            for(int i = before - 1; 0 <= i; i--) {
                if(p(this.Points[i].Core) is not null) {
                    return i;
                }
            }
            return 0;
        }

        /// <summary><paramref name="after"/> より後で <paramref name="p"/> が値を返す最初の位置</summary>
        public int FirstAfter(int after, Probe<int> p) {
            for(int i = after + 1; i <= this.Last; i++) {
                if(p(this.Points[i].Core) is not null) {
                    return i;
                }
            }
            return this.Last;
        }
    }

    private static Scan ForwardUntilFound(Timeline t, Probe<int> p) {
        return Until(() => t.Forward(p, out _));
    }

    private static Scan BackwardUntilFound(Timeline t, Probe<int> p) {
        return Until(() => t.Backward(p, out _));
    }

    /// <summary>区間ごとの探索を <see cref="Scan.Boundary"/> 以外になるまで繰り返す</summary>
    /// <remarks>動かなくなった実装でもテストが終わるよう, 区間の数より十分に多い回数で打ち切る</remarks>
    private static Scan Until(Func<Scan> scan) {
        for(int i = 0; i < 1000; i++) {
            if(scan() is Scan s and not Scan.Boundary) {
                return s;
            }
        }
        Assert.Fail("The search did not finish.");
        return default;
    }

    [Fact]
    public void EachStepMatchesReferenceInBothDirections() {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, new ArrayBufferWriter<byte>());
        for(int i = 1; i <= r.Last; i++) {
            Assert.Equal(Scan.Hit, t.Forward(Any, out _));
            r.AssertAt(i, t);
        }
        for(int i = r.Last - 1; 0 <= i; i--) {
            Assert.Equal(Scan.Hit, t.Backward(Any, out _));
            r.AssertAt(i, t);
        }
        Assert.Equal(Scan.Start, t.Backward(Any, out _));
        r.AssertAt(0, t);
    }

    [Fact]
    public void ForwardAtHaltJudgesWithoutMoving() {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, new ArrayBufferWriter<byte>());
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        r.AssertAt(r.Last, t);
        Assert.Equal(Scan.Hit, t.Forward(Any, out _));
        r.AssertAt(r.Last, t);
        Assert.Equal(Scan.Hit, t.Backward(Any, out _));
        r.AssertAt(r.Last - 1, t);
    }

    [Theory]
    [InlineData(1234u)]
    [InlineData(4999u)]
    [InlineData(1u)]
    public void BackwardStopsAtLastMatchBeforeCurrent(uint value) {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, new ArrayBufferWriter<byte>());
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        Probe<int> p = S0(value);
        int at = r.Last;
        // 値が続く位置では, 2回目は直前の位置で止まる
        for(int k = 0; k < 2; k++) {
            at = r.LastBefore(at, p);
            Assert.Equal(Scan.Hit, BackwardUntilFound(t, p));
            r.AssertAt(at, t);
        }
    }

    [Fact]
    public void BackwardWithoutMatchReachesStart() {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, new ArrayBufferWriter<byte>());
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        Assert.Equal(Scan.Start, BackwardUntilFound(t, Never));
        r.AssertAt(0, t);
    }

    [Fact]
    public void ForwardFromPastStopsAtFirstMatchWithoutRepeatingOutput() {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        ArrayBufferWriter<byte> output = new();
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, output);
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        Assert.Equal(r.Output, output.WrittenSpan.ToArray());
        Assert.Equal(Scan.Start, BackwardUntilFound(t, Never));

        Probe<int> p = S0(1234);
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, p));
        r.AssertAt(r.FirstAfter(0, p), t);
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        r.AssertAt(r.Last, t);
        Assert.Equal(r.Output, output.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomWalkMatchesReference(int seed) {
        ProgramImage image = Assemble(Loop, Handler);
        Reference r = Reference.Of(image);
        ArrayBufferWriter<byte> output = new();
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, output);
        Random rng = new(seed);
        int at = 0;
        for(int k = 0; k < 200; k++) {
            int n = rng.Next(1, 3 * 4096);
            if(rng.Next(2) == 0) {
                for(int i = 0; i < n && 0 < at; i++) {
                    Assert.Equal(Scan.Hit, t.Backward(Any, out _));
                    at--;
                }
            } else {
                for(int i = 0; i < n; i++) {
                    Assert.Equal(Scan.Hit, t.Forward(Any, out _));
                    at = Math.Min(at + 1, r.Last);
                }
            }
            r.AssertAt(at, t);
        }
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        Assert.Equal(r.Output, output.WrittenSpan.ToArray());
    }

    [Fact]
    public void NeedInputKeepsPositionAndReplayReusesInput() {
        ProgramImage image = Assemble(Echo, Handler);
        Reference r = Reference.Of(image, "ab");
        Timeline t = Timeline.Load(image, DelaySlotsConfig.All, new ArrayBufferWriter<byte>());
        Assert.Equal(Scan.NeedInput, ForwardUntilFound(t, Exit));
        Point waiting = Observe(t.View);
        Assert.Equal(Scan.NeedInput, t.Forward(Exit, out _));
        AssertSame(waiting, Observe(t.View));

        t.Feed("ab"u8);
        t.CloseInput();
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        r.AssertAt(r.Last, t);
        Assert.Equal((uint)('a' + 'b'), t.View.Core.Registers[GprId.S0]);

        Assert.Equal(Scan.Start, BackwardUntilFound(t, Never));
        Assert.Equal(Scan.Hit, ForwardUntilFound(t, Exit));
        r.AssertAt(r.Last, t);
    }
}
