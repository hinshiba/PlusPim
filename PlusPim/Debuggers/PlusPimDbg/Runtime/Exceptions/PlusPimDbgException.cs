namespace PlusPim.Debuggers.PlusPimDbg.Runtime.Exceptions;

/// <summary>
/// このランタイム内での例外の基底クラス
/// </summary>
internal abstract class PlusPimDbgException: Exception {
    protected PlusPimDbgException() { }
    protected PlusPimDbgException(string message) : base(message) { }
    protected PlusPimDbgException(string message, Exception innerException) : base(message, innerException) { }
}
