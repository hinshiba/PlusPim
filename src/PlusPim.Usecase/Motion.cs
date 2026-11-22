namespace PlusPim.Usecase;

/// <summary>
/// 実行の操作
/// </summary>
public readonly union Motion(StepIn, StepOver, StepOut, Continue, StepBack, ReverseContinue);

/// <summary>次の単位まで進む</summary>
public readonly record struct StepIn(Granularity Granularity);

/// <summary>呼び出しを飛び越えて次の単位まで進む</summary>
public readonly record struct StepOver(Granularity Granularity);

/// <summary>現在のフレームから抜けるまで進む</summary>
public readonly record struct StepOut(Granularity Granularity);

/// <summary>止まる理由が発生するまで進む</summary>
public readonly record struct Continue;

/// <summary>1単位戻る</summary>
public readonly record struct StepBack(Granularity Granularity);

/// <summary>ブレークポイントか履歴の先頭まで戻る</summary>
public readonly record struct ReverseContinue;
