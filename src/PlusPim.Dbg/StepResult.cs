namespace PlusPim.Dbg;

/// <summary>
/// ステップ実行の結果
/// </summary>
public enum StepResult: byte {
    /// <summary>1ステップ進んだ</summary>
    Stepped,
    /// <summary>入力待ち</summary>
    WaitInput,
    /// <summary>停止している</summary>
    Halted,
}
