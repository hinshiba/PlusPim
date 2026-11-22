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
    public bool Reached(StateView v, Rules r) {
        return this.Granularity is Granularity g && v.Depth <= this.MaxDepth && r.OnBoundary(g, v);
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
    public bool OnBreakpoint(StateView v) {
        return v.Status is Running && this.Breakpoints.Contains(v.Pc);
    }

    /// <summary>実行中で, <paramref name="g"/> の単位の境界にいる</summary>
    public bool OnBoundary(Granularity g, StateView v) {
        return v.Status is Running && (g == Granularity.Instruction || this.Lines.StartsLine(v.Pc));
    }
}

/// <summary>
/// 1ステップごとに止まるかを決める
/// </summary>
internal static class Judge {
    /// <summary>
    /// 1ステップ進めた, または進めなかった後の判定
    /// </summary>
    /// <returns>止まらないなら <see langword="null"/></returns>
    public static Stop? Forward(Goal g, StateView v, Rules r) {
        return v.Status switch {
            Running => Arrived(g, v, r),
            ExceptionRaised t => r.Catches(t.Code) ? t : null,
            Exited e => e,
            RuntimeErrorRaised f => f,
        };
    }

    /// <summary>
    /// 1ステップ戻った後の判定. 実行していないので例外では止まらない
    /// </summary>
    /// <param name="moved">戻れたか</param>
    public static Stop? Backward(bool moved, Goal g, StateView v, Rules r) {
        return moved ? Arrived(g, v, r) : new HistoryStart();
    }

    /// <summary>
    /// 移った先の位置による判定. 両方向で共通で, ブレークポイントを目標より優先する
    /// </summary>
    /// <returns>止まらないなら <see langword="null"/></returns>
    private static Stop? Arrived(Goal g, StateView v, Rules r) {
        return r.OnBreakpoint(v) ? new Breakpoint()
            : g.Reached(v, r) ? new Stepped()
            : null;
    }
}
