namespace PlusPim.Instructions;

/// <summary>
/// ランタイムエラーの種類
/// </summary>
public enum RuntimeErrorKind: byte {
    /// <summary><c>div</c>/<c>divu</c> のゼロ除算</summary>
    DivisionByZero,
    /// <summary><c>runtime_call!</c> で未知の機能番号</summary>
    UnknownRuntimeCall,
}
