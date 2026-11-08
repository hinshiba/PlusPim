namespace PlusPim.Instructions;

public readonly record struct Immediate(int Value);

public readonly record struct Memory(RegisterId BaseRegId, int Offset);

public readonly union Operand(RegisterId, Immediate, Memory, Address);
