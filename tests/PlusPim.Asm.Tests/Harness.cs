using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Buffers;

namespace PlusPim.Asm.Tests;

/// <summary>
/// メモリ上のソースをアセンブルし, 公開された結果だけを観測する
/// </summary>
internal static class Harness {
    public static readonly Address T = MemoryMap.Text.Base;
    public static readonly Address D = MemoryMap.Data.Base;

    /// <summary>エントリの <c>main</c> をテキストの先頭に置く. 続く本体は4行目から始まる</summary>
    public const string Head = ".text\n.globl main\nmain:\n";

    private sealed class Reader(IReadOnlyDictionary<SourcePath, string> files): ISourceReader {
        public string? Read(SourcePath path) {
            return files.GetValueOrDefault(path);
        }
    }

    /// <summary><paramref name="index"/> 番目のソースのパス</summary>
    public static SourcePath PathOf(int index) {
        return SourcePath.TryResolve($"f{index}.asm", AppContext.BaseDirectory, out SourcePath p) ? p : throw new InvalidOperationException();
    }

    /// <summary><paramref name="sources"/> を <see cref="PathOf"/> の順に並べてアセンブルする</summary>
    public static AssemblyResult Assemble(params string[] sources) {
        SourcePath[] paths = [.. sources.Select((_, i) => PathOf(i))];
        return Assemble(paths, paths.Zip(sources).ToDictionary());
    }

    /// <summary><paramref name="files"/> から読んで <paramref name="paths"/> の順にアセンブルする</summary>
    /// <param name="paths">配置する順のソース</param>
    /// <param name="files">読めるソース. ないパスは読めない</param>
    public static AssemblyResult Assemble(IEnumerable<SourcePath> paths, IReadOnlyDictionary<SourcePath, string> files) {
        return Assembler.Assemble(paths, new Reader(files));
    }

    /// <summary>出現順の診断のコード</summary>
    public static object[] Codes(this AssemblyResult r) {
        return [.. r.Diagnostics.Select(d => d.Code.Value!)];
    }

    public static void AssertCodes(this AssemblyResult r, params object[] expected) {
        Assert.Equal(expected, r.Codes());
    }

    /// <summary>エラーで失敗し, そのコードに <paramref name="code"/> を含む</summary>
    public static void AssertFails(this AssemblyResult r, ErrorCode code) {
        Assert.Null(r.Program);
        Assert.Contains(code, r.Codes());
    }

    /// <summary>テキストの先頭から <paramref name="index"/> 番目の命令</summary>
    public static IExecute Fetch(this AssemblyResult r, int index) {
        return r.FetchAt(T + (uint)(index * 4));
    }

    public static IExecute FetchAt(this AssemblyResult r, Address addr) {
        return r.Prog().Image.Fetch(addr) ?? throw new Xunit.Sdk.XunitException($"No instruction at {addr}.");
    }

    /// <summary>エラーがなく作られたプログラム</summary>
    public static AssembledProgram Prog(this AssemblyResult r) {
        return r.Program ?? throw new Xunit.Sdk.XunitException($"Assembly failed: {string.Join(", ", r.Codes())}.");
    }

    /// <summary><paramref name="addr"/> から始まるデータセクションのバイト列. なければ空</summary>
    public static string DataAt(this AssemblyResult r, Address addr) {
        return Convert.ToHexString(r.Prog().Image.Data.SingleOrDefault(d => d.Base == addr).Bytes.Span);
    }

    /// <summary>実命令の一覧表から直接作った命令</summary>
    public static IExecute I(string mnemonic, params Operand[] operands) {
        return InstructionSet.ByMnemonic[mnemonic](operands) ?? throw new ArgumentException($"{mnemonic} rejects the operands.");
    }

    public static Immediate Imm(int v) {
        return new Immediate(unchecked((ushort)v));
    }

    public static void AssertSame(IExecute expected, IExecute actual) {
        Assert.Equal(expected.Text, actual.Text);
    }

    /// <summary>
    /// テキストの先頭から <paramref name="count"/> 命令を順に評価し, 汎用レジスタを返す
    /// </summary>
    /// <remarks>疑似命令の展開を確かめるためのもので, 汎用レジスタの読み書きだけを扱う</remarks>
    public static uint[] Run(this AssemblyResult r, int count, params (GprId Reg, uint Value)[] init) {
        uint[] gpr = new uint[32];
        foreach((GprId reg, uint value) in init) {
            gpr[(int)reg] = value;
        }
        for(int i = 0; i < count; i++) {
            IExecute x = r.Fetch(i);
            uint[] src = [.. x.Src.ToArray().Select(n => n.Value is GprId g ? gpr[(int)g] : 0u)];
            uint[] dest = new uint[x.Dest.Length];
            x.Eval(src, dest);
            for(int j = 0; j < dest.Length; j++) {
                if(x.Dest[j].Value is GprId g and not GprId.Zero) {
                    gpr[(int)g] = dest[j];
                }
            }
        }
        return gpr;
    }

    /// <summary>
    /// エントリからランタイムで実行し, 命令のない番地に達した状態を返す
    /// </summary>
    /// <param name="r">実行するプログラム</param>
    /// <param name="slots">有効にする遅延スロット</param>
    /// <remarks>メモリ, PC, Hi/Loを読み書きする疑似命令の展開を確かめるためのもの</remarks>
    public static StateView Exec(this AssemblyResult r, DelaySlotsConfig slots = DelaySlotsConfig.All) {
        ProgramImage image = r.Prog().Image;
        Debuggee d = Debuggee.Load(image, slots);
        ArrayBufferWriter<byte> output = new();
        for(int i = 0; image.Fetch(d.View.Core.Pc) is not null; i++) {
            Assert.True(i < 64, "Execution did not reach the end of the text.");
            Assert.Equal(StepResult.Stepped, d.Step(output));
            Assert.True(d.View.Core.Status is Running, $"Stopped by {d.View.Core.Status}.");
        }
        return d.View;
    }

    /// <summary><c>la $t0, label</c> を末尾に足して評価し, ラベルのアドレスを得る</summary>
    /// <param name="source">先頭のファイル. テキストの先頭から <paramref name="before"/> 命令の後に <c>la</c> を置く</param>
    /// <param name="before">テキストの先頭から <c>la</c> までの命令数</param>
    /// <param name="label">アドレスを得るラベル</param>
    /// <param name="others">後に続くファイル</param>
    public static uint AddressOf(string source, int before, string label, params string[] others) {
        AssemblyResult r = Assemble([source + $"\n.text\nla $t0, {label}", .. others]);
        return r.Run(before + 2)[(int)GprId.T0];
    }
}
