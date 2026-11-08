namespace PlusPim.Instructions;

/// <summary>
/// 命令の文字列からの解析に必要な情報の抽象化
/// </summary>
public interface IAssemblerable {

    static abstract string Mnemonic { get; }

    static abstract IExecute? FromOperands(ReadOnlySpan<Operand> operands, int sourceLine);
}
