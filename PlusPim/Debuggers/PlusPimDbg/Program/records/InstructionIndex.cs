namespace PlusPim.Debuggers.PlusPimDbg.Program.records;

/// <summary>
/// 命令インデックスを表す値型
/// </summary>
/// <param name="Idx">命令インデックス</param>
internal readonly record struct InstructionIndex(int Idx);
