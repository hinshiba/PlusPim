using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System.Collections.Concurrent;

/// <summary>
/// BDN が測らない値をイテレーションごとに集める．InProcess で動かすのでホストと共有できる
/// </summary>
public static class Samples {
    private static readonly ConcurrentDictionary<string, List<long>> ByKey = [];

    /// <summary>
    /// <paramref name="type"/> の <paramref name="method"/> の1イテレーションの値を記録する
    /// </summary>
    /// <param name="parameters">ベンチマークのパラメータの値．<see cref="KeyOf(BenchmarkCase)"/> と同じ順</param>
    public static void Record(string type, string method, string parameters, long value) {
        ByKey.GetOrAdd($"{type}.{method}/{parameters}", _ => []).Add(value);
    }

    /// <summary><paramref name="c"/> に対応する記録のキー</summary>
    public static string KeyOf(BenchmarkCase c) {
        return $"{c.Descriptor.Type.Name}.{c.Descriptor.WorkloadMethod.Name}/{string.Join(",", c.Parameters.Items.Select(p => p.Value))}";
    }

    /// <summary>記録の中央値</summary>
    public static double? Median(BenchmarkCase c) {
        if(!ByKey.TryGetValue(KeyOf(c), out List<long>? v)) {
            return null;
        }
        long[] s = [.. v.Order()];
        return (s[(s.Length - 1) / 2] + s[s.Length / 2]) / 2.0;
    }

    /// <summary>
    /// クラスごとに <c>BenchmarkDotNet.Artifacts/results/{クラス}-samples.csv</c> へ書き出す
    /// </summary>
    public static void Export() {
        foreach(IGrouping<string, KeyValuePair<string, List<long>>> g in ByKey.GroupBy(kv => kv.Key[..kv.Key.IndexOf('.')])) {
            string dir = Directory.CreateDirectory(Path.Combine("BenchmarkDotNet.Artifacts", "results")).FullName;
            File.WriteAllLines(Path.Combine(dir, $"{g.Key}-samples.csv"),
                ["Key,Value", .. g.SelectMany(kv => kv.Value.Select(v => $"\"{kv.Key}\",{v}"))]);
        }
    }
}

/// <summary>
/// <see cref="Samples"/> の中央値をサマリの列にする
/// </summary>
/// <param name="name">列名</param>
/// <param name="format">中央値の表示．対象外のベンチマークなら <see langword="null"/></param>
public sealed class SampleColumn(string name, Func<double, BenchmarkCase, string?> format): IColumn {
    public string Id => $"{nameof(SampleColumn)}.{name}";
    public string ColumnName => name;
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => $"{name} (median of iterations)";

    public string GetValue(Summary summary, BenchmarkCase c) {
        return Samples.Median(c) is double m ? format(m, c) ?? "-" : "-";
    }

    public string GetValue(Summary summary, BenchmarkCase c, SummaryStyle style) {
        return this.GetValue(summary, c);
    }

    public bool IsAvailable(Summary summary) {
        return true;
    }

    public bool IsDefault(Summary summary, BenchmarkCase c) {
        return false;
    }
}
