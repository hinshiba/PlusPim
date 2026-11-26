namespace PlusPim.Instructions;

/// <summary>
/// 命令が起こすランタイムエラーの種類
/// </summary>
public enum RuntimeErrorKind: byte {
    /// <summary><c>div</c>/<c>divu</c> でゼロ除算を実行した. 未定義動作</summary>
    DivisionByZero,
    /// <summary><c>runtime_call!</c> で未知の機能番号</summary>
    UnknownRuntimeCall,
    /// <summary>命令の無いセグメント内の番地を実行した. データを命令として解釈することは未対応</summary>
    NonInstructionExecution,
    /// <summary>テキストのメモリを読み書きした. 命令をデータとして解釈することは未対応</summary>
    TextAccess,
    /// <summary>遅延スロットで分岐, ジャンプ, <c>eret</c> を実行した. 未定義動作/summary>
    TransferInDelaySlot,
}
