namespace PlusPim.Instructions;

/// <summary>
/// オペランドから命令を作る. オペランドの形が合わなければ <see langword="null"/>
/// </summary>
public delegate IExecute? FromOperands(ReadOnlySpan<Operand> operands);

/// <summary>
/// 命令の文字列からの解析に必要な情報の抽象化
/// </summary>
public interface IAssemblerable {

    static abstract string Mnemonic { get; }

    static abstract IExecute? FromOperands(ReadOnlySpan<Operand> operands);
}
