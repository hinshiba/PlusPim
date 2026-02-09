using PlusPim.Debuggers.PlusPimDbg.Instructions;
using System.Diagnostics.CodeAnalysis;

namespace PlusPim.Debuggers.PlusPimDbg;

internal sealed class Mnemonic: IParsable<Mnemonic> {
    internal IInstruction Instruction { get; }

    private Mnemonic(IInstruction instruction) {
        this.Instruction = instruction;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Mnemonic result) {
        result = null;
        if(s is null) {
            return false;
        }

        if(InstructionRegistry.Default.TryParse(s, out IInstruction? instruction)) {
            result = new Mnemonic(instruction);
            return true;
        }

        return false;
    }

    static Mnemonic IParsable<Mnemonic>.Parse(string s, IFormatProvider? provider) {
        return Mnemonic.TryParse(s, provider, out Mnemonic? result) ? result : throw new FormatException();
    }

    public void Execute(ExecuteContext context) {
        this.Instruction.Execute(context);
    }

    public void Undo(ExecuteContext context) {
        this.Instruction.Undo(context);
    }
}

internal enum RegisterID {
    Zero,
    At,
    V0,
    V1,
    A0,
    A1,
    A2,
    A3,
    T0,
    T1,
    T2,
    T3,
    T4,
    T5,
    T6,
    T7,
    S0,
    S1,
    S2,
    S3,
    S4,
    S5,
    S6,
    S7,
    T8,
    T9,
    K0,
    K1,
    Gp,
    Sp,
    S8,
    Ra
}
