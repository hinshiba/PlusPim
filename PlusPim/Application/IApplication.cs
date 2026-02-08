namespace PlusPim.Application;

internal interface IApplication {
    void SetLogger(Action<string> log);
    bool Load(string programPath);
    (int[] Registers, int PC, int HI, int LO) GetRegisters();
    void Step();

    /// <summary>
    /// 1ステップ分、実行を巻き戻す
    /// </summary>
    /// <returns>巻き戻しに成功した場合は<see langword="true"/></returns>
    bool StepBack();

    /// <summary>
    /// 現在の実行前の行を取得する
    /// </summary>
    /// <returns>1から始まる行番号．取得できない場合は0を返す．</returns>
    int GetCurrentLine();
    string GetProgramPath();
    bool IsTerminated();
}
