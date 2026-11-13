namespace PlusPim.Instructions;

/// <summary>
/// ランタイムエラーの種類
/// </summary>
public enum RuntimeErrorKind: byte {
    /// <summary><c>div</c>/<c>divu</c> のゼロ除算</summary>
    DivisionByZero,
    /// <summary><c>div</c> の <c>0x80000000 / -1</c></summary>
    DivisionOverflow,
    /// <summary><c>runtime_call!</c> で未知の機能番号</summary>
    UnknownRuntimeCall,
}
