/// <summary>
/// ベンチマークが読むアセンブリファイル
/// </summary>
public static class Workload {
    /// <summary>
    /// リポジトリの根から <paramref name="relative"/> のファイルを探す
    /// </summary>
    /// <param name="relative">リポジトリの根からの相対パス</param>
    /// <returns>絶対パス</returns>
    public static string Locate(string relative) {
        for(DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent) {
            string p = Path.Combine(d.FullName, relative);
            if(File.Exists(p)) {
                return p;
            }
        }
        throw new FileNotFoundException(relative);
    }

    /// <summary>
    /// およそ <paramref name="n"/> 命令を実行する loop.asm を書き出す
    /// </summary>
    /// <param name="n">命令数</param>
    /// <returns>書き出したパスと，ブレークポイントを置く 1-indexed の行</returns>
    public static (string Path, int DoneLine) Write(int n) {
        string[] lines = File.ReadAllLines(Locate("benchmarks/workloads/loop.asm"));
        int iter = n / 3; // 1反復3命令
        for(int i = 0; i < lines.Length; i++) {
            lines[i] = lines[i].Replace("33333", iter.ToString());
        }
        string dir = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pluspim-bench")).FullName;
        string path = System.IO.Path.Combine(dir, $"loop{n}.asm");
        File.WriteAllLines(path, lines);
        // done: の次の行の nop
        return (path, Array.FindIndex(lines, l => l.StartsWith("done:")) + 2);
    }
}
