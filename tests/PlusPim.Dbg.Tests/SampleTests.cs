using System.Buffers;
using System.Text;

namespace PlusPim.Dbg.Tests;

/// <summary>
/// <c>samples/</c> の各サンプルを対応する遅延スロットの設定で最後まで実行し, 出力を照合する
/// </summary>
public class SampleTests {
    private static readonly (string Name, string Output)[] Expected = [
        ("array", "sum = 35\nmax = 9\n"),
        ("bubble", "before: 5 3 9 1 7 2 8 \nafter : 1 2 3 5 7 8 9 \n"),
        ("fact", "The factorial of 10 is 3628800\n"),
        ("fib", "fib(10) = 55\n"),
        ("gcd", "gcd(1071, 462) = 21\nlcm(1071, 462) = 23562\n"),
        ("strlen", "length = 15\nHELLO, PLUSPIM!\n"),
        ("sum", "sum(1..10) = 55\n"),
        ("syscall", "Hello PlusPim!"),
    ];

    private static readonly (string Dir, DelaySlotsConfig Slots)[] Modes = [
        ("delay-slots", DelaySlotsConfig.All),
        ("no-delay-slots", DelaySlotsConfig.None),
    ];

    public static TheoryData<string, DelaySlotsConfig, string, string> Cases() {
        TheoryData<string, DelaySlotsConfig, string, string> data = [];
        foreach((string dir, DelaySlotsConfig slots) in Modes) {
            foreach((string name, string output) in Expected) {
                data.Add(dir, slots, name, output);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RunsToReturn(string dir, DelaySlotsConfig slots, string name, string output) {
        string root = Path.Combine(AppContext.BaseDirectory, "samples", dir);
        Debuggee d = Debuggee.Load(Harness.Assemble(File.ReadAllText(Path.Combine(root, name + ".asm")), File.ReadAllText(Path.Combine(root, "kseg.asm"))), slots);
        ArrayBufferWriter<byte> written = new();
        for(int i = 0; i < 100_000 && d.Step(written) == StepResult.Stepped; i++) {
        }
        Assert.Equal(new Exited(ExitCause.Returned), d.View.Core.Status);
        Assert.Equal(output, Encoding.UTF8.GetString(written.WrittenSpan));
    }
}
