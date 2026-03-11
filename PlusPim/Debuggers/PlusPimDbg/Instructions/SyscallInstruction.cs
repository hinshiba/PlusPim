using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instructions;

internal class SyscallInstruction(int sourceLine): IInstruction {
    public int SourceLine => sourceLine;

    public void Execute(ExecuteContext context) {
        switch(context.Registers[RegisterID.V0]) {
            case (int)SyscallCode.PrintInt:
                context.Log($"Syscall: print_int {context.Registers[RegisterID.A0]}");

                Console.WriteLine(context.Registers[RegisterID.A0]);
                break;

            case (int)SyscallCode.PrintString:
                context.Log($"Syscall: print_string at address 0x{context.Registers[RegisterID.A0]:X8}");

                Address readAddr = new(context.Registers[RegisterID.A0]);
                List<byte> bytes = [];

                for(byte b; (b = context.ReadMemoryByte(readAddr++)) != 0;) {
                    bytes.Add(b);
                }
                Console.WriteLine(System.Text.Encoding.UTF8.GetString([.. bytes]));
                break;

            case (int)SyscallCode.ReadInt:
                context.Log("Syscall: read_int to $a0");

                if(int.TryParse(Console.ReadLine(), out int value)) {
                    context.Registers[RegisterID.A0] = value;
                } else {
                    context.Log("Syscall: Invalid input for read_int, so set 0");
                    context.Registers[RegisterID.A0] = 0;
                }
                break;

            case (int)SyscallCode.ReadString:
                context.Log($"Syscall: read_string to address 0x{context.Registers[RegisterID.A0]:X8}");

                string input = Console.ReadLine() ?? "";
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

                Address writeAddr = new(context.Registers[RegisterID.A0]);
                foreach(byte b in inputBytes) {
                    context.WriteMemoryByte(writeAddr, b);
                    writeAddr++;
                }
                context.Registers[RegisterID.A1] = inputBytes.Length; // $a1に入力したバイト数をセット
                break;

            case (int)SyscallCode.Exit:
                context.Log("Syscall: exit");
                // TODO
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
    ReadInt = 5,
    ReadString = 8,
    Exit = 10,
}

/// <summary>
/// Syscallのパーサー．他の命令と違いオペランドを取らないため，<see cref="InstructionRegistry"/>で特別扱いされる"/>
/// </summary>
internal static class SyscallInstructionParser {
    public static string Mnemonic => "syscall";
}
