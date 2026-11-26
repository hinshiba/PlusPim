namespace PlusPim.Conformance.Tests;

/// <summary>
/// SingleStepTests/r3000 の命令ごとのファイルを実行し, 既知の差を除いてR3000と一致することを確かめる
/// </summary>
/// <remarks>データは <c>scripts/test/fetch-sst.sh</c> で <c>artifacts/sst/r3000</c> に取得する</remarks>
public class SstTests {
    /// <summary>一致しないケースを失敗のメッセージに載せる数</summary>
    private const int Shown = 10;

    public static TheoryData<string> Files() {
        return [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "sst-r3000.sha256")).Select(l => l.Split("  ")[1])];
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void MatchesR3000(string file) {
        string path = Path.Combine(DataDirectory(), file);
        if(!File.Exists(path)) {
            Assert.Skip($"{path} がない. scripts/test/fetch-sst.sh で取得する");
        }
        SstCase[] cases = SstCase.Parse(File.ReadAllBytes(path));
        (SstCase Case, SstOutcome Outcome)[] results = [.. cases.Select(c => (c, SstRunner.Run(c)))];

        string summary = string.Join(", ", results.GroupBy(r => r.Outcome.Value switch {
            SstPassed => "passed",
            SstSkipped s => $"skipped ({s.Reason})",
            SstKnown k => $"known ({k.Reason})",
            _ => "mismatch",
        }).Select(g => $"{g.Key}: {g.Count()}"));
        TestContext.Current.TestOutputHelper?.WriteLine($"{file}: {summary}");

        string[] mismatches = [.. results.Where(r => r.Outcome.Value is SstMismatch)
            .Select(r => $"{r.Case.Name} {r.Case.Opcode:X8} @{r.Case.Address:X8}: {((SstMismatch)r.Outcome.Value!).Detail}")];
        Assert.True(mismatches.Length == 0, $"{summary}\n{string.Join('\n', mismatches.Take(Shown))}");
    }

    /// <summary>環境変数 <c>PLUSPIM_SST_DIR</c>, なければリポジトリの <c>artifacts/sst/r3000</c></summary>
    private static string DataDirectory() {
        if(Environment.GetEnvironmentVariable("PLUSPIM_SST_DIR") is string dir) {
            return dir;
        }
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        while(d is not null && !File.Exists(Path.Combine(d.FullName, "PlusPim.slnx"))) {
            d = d.Parent;
        }
        return Path.Combine(d?.FullName ?? AppContext.BaseDirectory, "artifacts", "sst", "r3000");
    }
}
