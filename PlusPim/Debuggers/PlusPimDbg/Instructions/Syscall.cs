using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal class Syscall(int sourceLine): IInstruction {
    public int SourceLine => sourceLine;

    public void Execute(ExecuteContext context) {
        switch(context.Registers[RegisterID.V0]) {
            case (int)SyscallCode.PrintInt:
                context.Log($"Syscall: print_int {context.Registers[RegisterID.A0]}");
                break;
            case (int)SyscallCode.PrintString:
                context.Log($"Syscall: print_string at address 0x{context.Registers[RegisterID.A0]:X8}");
                break;
            case (int)SyscallCode.Exit:
                context.Log("Syscall: exit");
                break;
            default:
                context.Log($"Syscall: unknown code {context.Registers[RegisterID.V0]}");
                break;
        }
    }
    public void Undo(ExecuteContext context) {
        context.Log($"Syscall: print_int {context.Registers[RegisterID.A0]}");
    }
}


internal enum SyscallCode {
    PrintInt = 1,
    PrintString = 4,
    Exit = 10,
}
