using PlusPim.Adapter.Controller.Dap.Protocol;
using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using PlusPim.Usecase;
using Frame = PlusPim.Usecase.StackFrame;

namespace PlusPim.Adapter.Controller.Dap;

/// <summary>
/// 停止中に1つのスコープとして見せる変数の並び
/// </summary>
/// <param name="Name">スコープの名前</param>
/// <param name="Variables">変数. 子を持たない</param>
internal sealed record ScopeView(string Name, Variable[] Variables);

/// <summary>
/// Usecaseの値からDAPの値への対応. どれも純粋な関数である
/// </summary>
internal static class Mapping {
    private static readonly ExcCode[] Fatal = [ExcCode.AdEL, ExcCode.AdES, ExcCode.RI, ExcCode.CpU, ExcCode.Ov];

    /// <summary><c>initialize</c> で見せる例外フィルタ</summary>
    public static ExceptionBreakpointsFilter[] Filters { get; } = [
        Filter(ExceptionFilter.Fatal, "Fatal Exceptions", "Break when an (commonly) unrecoverable exception occurs", true),
        Filter(ExceptionFilter.Break, "Break Exceptions", "Break on break instruction", true),
        Filter(ExceptionFilter.Syscall, "Syscall Exceptions", "Break on syscall instruction", false),
    ];

    private static ExceptionBreakpointsFilter Filter(ExceptionFilter f, string label, string description, bool on) {
        return new(f, label, $"{description} ({string.Join(", ", Caught(f).Select(c => MessageCatalog.Describe(c).Id))})", on);
    }

    private static ExcCode[] Caught(ExceptionFilter f) {
        return f switch {
            ExceptionFilter.Fatal => Fatal,
            ExceptionFilter.Break => [ExcCode.Bp],
            ExceptionFilter.Syscall => [ExcCode.Sys],
            _ => throw new ArgumentOutOfRangeException(nameof(f)),
        };
    }

    /// <summary>
    /// 停止を <c>stopped</c> イベントと, <c>exceptionInfo</c> で見せる識別子と説明にする
    /// </summary>
    /// <returns>
    /// <c>Event</c> は <c>terminated</c> を送る停止では <see langword="null"/>.
    /// <c>Exception</c> は例外とランタイムエラーのない停止では <see langword="null"/>
    /// </returns>
    public static (StoppedEvent? Event, ExceptionText? Exception) Translate(Stop s) {
        return s switch {
            Stepped => (new("step"), null),
            Usecase.Breakpoint => (new("breakpoint"), null),
            Paused => (new("pause"), null),
            HistoryStart => (new("step", MessageCatalog.Describe(s)), null),
            Exited => (null, null),
            // 実行を打ち切る停止もセッションは続け, 巻き戻して原因を調べられるようにする
            ExceptionRaised t => Exception(s, MessageCatalog.Describe(t.Code).Id),
            RuntimeErrorRaised f => Exception(s, MessageCatalog.Describe(f.Error).Id),
        };
    }

    private static (StoppedEvent, ExceptionText) Exception(Stop s, string id) {
        string description = MessageCatalog.Describe(s)!;
        return (new("exception", description, id), new(id, description));
    }

    /// <summary>有効な例外フィルタが捕捉する例外</summary>
    public static ExcCode[] Codes(IEnumerable<ExceptionFilter> filters) {
        return [.. filters.Distinct().SelectMany(Caught)];
    }

    /// <summary>
    /// フレームのスコープを作る
    /// </summary>
    /// <param name="f">フレーム</param>
    /// <param name="live">ライブフレームか. CP0はライブフレームだけが持つ</param>
    public static ScopeView[] Scopes(in Frame f, bool live) {
        Registers r = f.Registers;
        // 値をアドレスとしてメモリビューを開けるようにする
        ScopeView general = new("General", [.. Enum.GetValues<GprId>().Select(g => Pointer(g.AsmName(), r[g]))]);
        ScopeView special = new("Special", [Value("pc", f.Pc.Addr), Value("hi", r[RName.Hi]), Value("lo", r[RName.Lo])]);
        ScopeView memory = new("Memory", [Pointer(".data", MemoryMap.Data.Base.Addr), Pointer("$sp", r[GprId.Sp]), Pointer("$gp", r[GprId.Gp])]);
        return live
            ? [general, special, new("CP0", [Value("BadVAddr", r[RName.BadVAddr]), Value("Status", r[RName.Status]), Value("Cause", r[RName.Cause]), Value("EPC", r[RName.Epc])]), memory]
            : [general, special, memory];
    }

    private static Variable Value(string name, uint value) {
        return new Variable(name, new Word(value), VariablesReference.None, null);
    }

    private static Variable Pointer(string name, uint value) {
        return new Variable(name, new Word(value), VariablesReference.None, new Address(value));
    }
}
