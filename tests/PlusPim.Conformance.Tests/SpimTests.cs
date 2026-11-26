using PlusPim.Asm;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;
using System.Text;

namespace PlusPim.Conformance.Tests;

/// <summary>
/// SPIMの回帰テスト <c>third_party/spim/</c> を, 除外表 <see cref="SpimSuite"/> を除いて実行し, failへ移らずにmainから戻ることを確かめる
/// </summary>
public class SpimTests {
    private sealed class Reader(Dictionary<SourcePath, string> files): ISourceReader {
        public string? Read(SourcePath path) {
            return files.GetValueOrDefault(path);
        }
    }

    public static TheoryData<string> Programs() {
        return [.. SpimSuite.Programs.Select(p => p.File)];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void PassesAllTests(string file) {
        SpimProgram p = SpimSuite.Programs.Single(p => p.File == file);
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "spim", file));
        SpimSource src = SpimSource.Of(lines, p.Excluded);
        string mode = p.Slots == DelaySlotsConfig.None ? "no-delay-slots" : "delay-slots";
        string kseg = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "kseg", mode, "kseg.asm"));
        SourcePath[] paths = [Resolve(file), Resolve("kseg.asm")];
        AssemblyResult r = Assembler.Assemble(paths, new Reader(new() { [paths[0]] = src.Text, [paths[1]] = kseg }));
        Assert.True(r.Diagnostics.IsEmpty, string.Join('\n', r.Diagnostics.Select(d => Describe(d, paths[0], src, lines))));

        DebugInfo info = r.Program!.DebugInfo;
        Debuggee d = Debuggee.Load(r.Program.Image, p.Slots);
        ArrayBufferWriter<byte> written = new();
        // failへ移る直前の2命令. 分岐と遅延スロットを含む
        (Address Before, Address Last) trail = default;
        string? failedAt = null;
        for(int i = 0; i < 10_000_000 && failedAt is null; i++) {
            Address pc = d.View.Core.Pc;
            if(d.Step(written) != StepResult.Stepped) {
                break;
            }
            trail = (trail.Last, pc);
            // 直前の分岐の遅延スロットとしてfailの先頭を実行する場合は移っていない
            if(info.SymbolAt(d.View.Core.Pc) == "fail" && d.View.Core.Branch is null) {
                failedAt = $"{Line(info, trail.Before, paths[0], src, lines)}\n{Line(info, trail.Last, paths[0], src, lines)}";
            }
        }
        Status status = d.View.Core.Status;
        // 成功するとmainから戻り, 失敗するとfailへ移る
        Assert.True(failedAt is null && status.Value is Exited(ExitCause.Returned),
            $"status: {status.Value} {(status.Value is RuntimeErrorRaised e ? e.Error.Value : null)}\n"
            + $"{failedAt ?? Line(info, d.View.Core.Pc, paths[0], src, lines)}\n{Encoding.UTF8.GetString(written.WrittenSpan)}");
    }

    private static SourcePath Resolve(string name) {
        return SourcePath.TryResolve(name, AppContext.BaseDirectory, out SourcePath p) ? p : throw new InvalidOperationException(name);
    }

    /// <summary><paramref name="addr"/> の命令を元のファイルの行で表す</summary>
    private static string Line(DebugInfo info, Address addr, SourcePath spim, SpimSource src, string[] lines) {
        if(info.SourceMap.LocationOf(addr) is not { } l || l.Path != spim) {
            return $"{addr}";
        }
        int line = src.Origin[l.Line - 1];
        return $"{addr}: L{line} {lines[line - 1].Trim()}";
    }

    /// <summary>診断を元のファイルの行で表す</summary>
    private static string Describe(Diagnostic d, SourcePath spim, SpimSource src, string[] lines) {
        string args = string.Join(", ", d.Args);
        if(d.Location is not { } l || l.Path != spim) {
            return $"{d.Code.Value} ({args})";
        }
        int line = src.Origin[l.Line - 1];
        return $"L{line}: {d.Code.Value} ({args}) {lines[line - 1].Trim()}";
    }
}
