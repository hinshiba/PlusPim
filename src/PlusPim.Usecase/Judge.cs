using PlusPim.Dbg;
using PlusPim.Image;
using PlusPim.Instructions;
using System.Collections.Frozen;

namespace PlusPim.Usecase;

/// <summary>
/// 実行の操作が目標に達して止まる条件
/// </summary>
/// <param name="Granularity">ステップの単位. 目標がなく止まる理由が起きるまで動くなら <see langword="null"/></param>
/// <param name="MaxDepth">止まれるコールスタックの深さの上限</param>
internal readonly record struct Goal(Granularity? Granularity, int MaxDepth) {
    /// <summary>止まる理由が起きるまで動く</summary>
    public static Goal None => new(null, 0);

    /// <summary>深さによらず次の単位で止まる</summary>
    public static Goal Any(Granularity g) {
        return new(g, int.MaxValue);
    }

    /// <summary>実行中で, 目標の深さ以下の単位の境界にいる</summary>
    public bool Reached(in Core c, Rules r) {
        return this.Granularity is Granularity g && c.Depth <= this.MaxDepth && r.OnBoundary(g, c);
    }
}

/// <summary>
/// 停止の判定に使う設定
/// </summary>
/// <param name="Breakpoints">ブレークポイントのアドレス</param>
/// <param name="Filter">捕捉する <see cref="ExcCode"/> のビット集合</param>
/// <param name="Lines">行の先頭の判定に使う</param>
internal readonly record struct Rules(FrozenSet<Address> Breakpoints, uint Filter, SourceMap Lines) {
    public bool Catches(ExcCode code) {
        return ((this.Filter >> (int)code) & 1) != 0;
    }

    /// <summary>実行中で, ブレークポイントの位置にいる</summary>
    public bool OnBreakpoint(in Core c) {
        return c.Status is Running && this.Breakpoints.Contains(c.Pc);
    }

    /// <summary>実行中で, <paramref name="g"/> の単位の境界にいる</summary>
    public bool OnBoundary(Granularity g, in Core c) {
        return c.Status is Running && (g == Granularity.Instruction || this.Lines.StartsLine(c.Pc));
    }
}

/// <summary>
/// 位置ごとに止まるかを決める. 判定はその位置の状態だけで決まり, 前後の位置によらない
/// </summary>
internal static class Judge {
    /// <summary>
    /// 1ステップ進めた, または進めなかった後の判定
    /// </summary>
    /// <returns>止まらないなら <see langword="null"/></returns>
    public static Stop? Forward(Goal g, in Core c, Rules r) {
        return c.Status switch {
            Running => Arrived(g, c, r),
            ExceptionRaised t => r.Catches(t.Code) ? t : null,
            Exited e => e,
            RuntimeErrorRaised f => f,
        };
    }

    /// <summary>
    /// 移った先の位置による判定. 両方向で共通で, ブレークポイントを目標より優先する.
    /// 戻るときは実行していないので例外では止まらず, これだけを使う
    /// </summary>
    /// <returns>止まらないなら <see langword="null"/></returns>
    public static Stop? Arrived(Goal g, in Core c, Rules r) {
        return r.OnBreakpoint(c) ? new Breakpoint()
            : g.Reached(c, r) ? new Stepped()
            : null;
    }
}
