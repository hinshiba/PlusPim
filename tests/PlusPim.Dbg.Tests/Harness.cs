using PlusPim.Asm;
using PlusPim.Image;
using System.Buffers;

namespace PlusPim.Dbg.Tests;

/// <summary>
/// ソースからデバッギを作って進める
/// </summary>
internal static class Harness {
    private sealed class Reader(Dictionary<SourcePath, string> files): ISourceReader {
        public string? Read(SourcePath path) {
            return files.GetValueOrDefault(path);
        }
    }

    /// <summary>エラーなくアセンブルできるソースを指定順に配置する</summary>
    public static ProgramImage Assemble(params string[] sources) {
        SourcePath[] paths = [.. sources.Select((_, i) => SourcePath.TryResolve($"f{i}.asm", AppContext.BaseDirectory, out SourcePath p) ? p : throw new InvalidOperationException())];
        return Assembler.Assemble(paths, new Reader(paths.Zip(sources).ToDictionary())).Program!.Image;
    }

    /// <summary><paramref name="n"/> ステップ進め, 現在の状態を返す</summary>
    public static Core Step(this Debuggee d, int n = 1) {
        for(int i = 0; i < n; i++) {
            Assert.Equal(StepResult.Stepped, d.Step(new ArrayBufferWriter<byte>()));
        }
        return d.View.Core;
    }
}
