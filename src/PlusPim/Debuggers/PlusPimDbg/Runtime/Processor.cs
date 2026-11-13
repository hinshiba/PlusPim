using PlusPim.Debuggers.PlusPimDbg.Instruction;
using System.Diagnostics;

namespace PlusPim.Debuggers.PlusPimDbg.Runtime;

/// <summary>
/// 1回の命令の実行の記録 (Undo用)
/// </summary>
/// <param name="Instruction">実行した命令．命令フェッチで例外が発生した場合は <see langword="null"/></param>
/// <param name="Before">実行前の例外・ランタイムエラーに関わる状態</param>
/// <param name="Result">実行結果</param>
internal sealed record ExecutionRecord(IInstruction? Instruction, ExceptionState Before, ExecuteResult Result) {
    /// <summary>
    /// 例外が発生したか
    /// </summary>
    public bool Raised => this.Result.Exception is not null;

    /// <summary>
    /// ランタイムエラーが発生したか
    /// </summary>
    public bool Failed => this.Result.Error is not null;

    /// <summary>
    /// 例外もランタイムエラーも発生せず，命令の実行が完了したか
    /// </summary>
    public bool Completed => !this.Raised && !this.Failed;
}

/// <summary>
/// 命令を実行し，例外・ランタイムエラーの適用と巻き戻しを行う処理系
/// </summary>
/// <remarks>
/// 例外・ランタイムエラーの適用と巻き戻しはこのクラスだけが行う．PC の自動インクリメントは行わない．
/// ランタイムエラーの発生後に実行を止めるのはデバッガの責務である
/// </remarks>
internal static class Processor {
    /// <summary>
    /// 命令を実行し，命令が要求した例外・ランタイムエラーを適用する
    /// </summary>
    public static ExecutionRecord Execute(RuntimeContext context, IInstruction instruction) {
        ExceptionState before = context.CaptureExceptionState();
        ExecuteResult result = instruction.Execute(context);
        Debug.Assert(result.Exception is null || result.Error is null, "An instruction must not request both an exception and a runtime error.");
        if(result.Exception is ExceptionRequest request) {
            // 例外を要求する命令は例外に関わる状態を変更してはならない
            Debug.Assert(context.CaptureExceptionState() == before, "An instruction that raises an exception must not change the exception state.");
            context.RaiseException(request.Code, request.BadVAddr);
        } else if(result.Error is RuntimeError error) {
            // ランタイムエラーを要求する命令も同様
            Debug.Assert(context.CaptureExceptionState() == before, "An instruction that raises a runtime error must not change the exception state.");
            context.RaiseRuntimeError(error);
        }
        return new ExecutionRecord(instruction, before, result);
    }

    /// <summary>
    /// 命令フェッチで発生した例外を適用する
    /// </summary>
    public static ExecutionRecord RaiseFetchFault(RuntimeContext context, ExceptionRequest fault) {
        ExceptionState before = context.CaptureExceptionState();
        context.RaiseException(fault.Code, fault.BadVAddr);
        return new ExecutionRecord(null, before, new ExecuteResult(fault, false));
    }

    /// <summary>
    /// 実行を取り消す．例外・ランタイムエラーに関わる状態 (CP0，LastException，IsTerminated，RuntimeError) も実行前に戻す
    /// </summary>
    public static void Undo(RuntimeContext context, ExecutionRecord record) {
        // 例外・ランタイムエラーを起こした命令は何も積んでいないので，命令のUndoは呼ばない
        if(record.Completed) {
            record.Instruction?.Undo(context);
        }
        context.RestoreExceptionState(record.Before);
    }
}
