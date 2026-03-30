using PlusPim.Debuggers.PlusPimDbg.Instruction.instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;

namespace PlusPim.Debuggers.PlusPimDbg.Instruction;

/// <summary>
/// カーネルモードのみから呼び出せるSyscall命令のハンドラ
/// </summary>
internal sealed class RuntimeCall(int sourceLine): IInstruction {
    /// <summary>
    /// 行番号
    /// </summary>
    public int SourceLine { get; } = sourceLine;


    private readonly Stack<SyscallCode> _history = new();
    private readonly Stack<uint> _prevReadInt = new();
    private readonly Stack<(Address, byte[])> _prevReadString = new();
    private readonly Stack<bool> _wasCpU = new();


    public void Execute(RuntimeContext context) {
        if(!context.IsKernelMode()) {
            // カーネル空間でないならコプロセッサ例外
            context.RaiseException(ExcCode.CpU);
            this._wasCpU.Push(true);
            return;
        }
        this._wasCpU.Push(false);

        SyscallCode code = (SyscallCode)context.Registers[RegisterID.V0];
        switch(code) {
            case SyscallCode.PrintInt:
                context.Log($"Syscall: print_int {context.Registers[RegisterID.A0]}");

                Console.Write(context.Registers[RegisterID.A0]);
                break;

            case SyscallCode.PrintString:
                context.Log($"Syscall: print_string at address 0x{context.Registers[RegisterID.A0]:X8}");

                Address readAddr = new(context.Registers[RegisterID.A0]);
                List<byte> bytes = [];

                for(byte b; (b = context.ReadMemoryByte(readAddr++)) != 0;) {
                    bytes.Add(b);
                }
                Console.Write(System.Text.Encoding.UTF8.GetString([.. bytes]));
                break;

            case SyscallCode.ReadInt:
                context.Log("Syscall: read_int to $v0");

                // Undoのために現在の値を保存
                this._prevReadInt.Push(context.Registers[RegisterID.V0]);

                // ユーザーからの入力を整数として読み取る
                if(int.TryParse(Console.ReadLine(), out int value)) {
                    context.Registers[RegisterID.V0] = (uint)value;
                } else {
                    context.Log("Syscall: Invalid input for read_int, so set 0");
                    context.Registers[RegisterID.V0] = 0;
                }
                break;

            case SyscallCode.ReadString:
                context.Log($"Syscall: read_string to address 0x{context.Registers[RegisterID.A0]:X8}");

                Address writeAddr = new(context.Registers[RegisterID.A0]);
                int maxLength = (int)context.Registers[RegisterID.A1];

                // Undoのためにメモリの内容を保存
                List<byte> prevBytes = [];
                for(int i = 0; i < maxLength + 1; i++) {
                    prevBytes.Add(context.ReadMemoryByte(writeAddr + i));
                }
                this._prevReadString.Push((writeAddr, [.. prevBytes]));

                // 実際に入力を受け取る
                string input = Console.ReadLine() ?? "";
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input)[0..maxLength];


                foreach(byte b in inputBytes) {
                    context.WriteMemoryByte(writeAddr, b);
                    writeAddr++;
                }
                context.WriteMemoryByte(writeAddr, 0); // null terminator
                break;

            case SyscallCode.Exit:
                context.Log("Syscall: exit");
                context.IsTerminated = true;
                break;

            default:
                context.Log($"Syscall: unknown code {context.Registers[RegisterID.V0]}");
                break;
        }
        this._history.Push(code);
    }
    public void Undo(RuntimeContext context) {
        if(this._wasCpU.Pop()) {
            context.RetException();
            return;
        }
        switch(this._history.Pop()) {
            case SyscallCode.PrintInt:
            case SyscallCode.PrintString:
                // 画面に出力した内容は消せないので無視
                break;

            case SyscallCode.ReadInt:
                // レジスタの値の復元
                context.Registers[RegisterID.V0] = this._prevReadInt.Pop();
                break;

            case SyscallCode.ReadString:
                // メモリの内容の復元
                (Address addr, byte[]? bytes) = this._prevReadString.Pop();
                foreach(byte b in bytes) {
                    context.WriteMemoryByte(addr++, b);
                }
                break;

            case SyscallCode.Exit:
                // フラグの復元
                context.IsTerminated = false;
                break;

            default:
                // 不明なコードは何もしていないので無視
                break;
        }
    }

    /// <summary>
    /// 命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineIndex) => {
            return OperandParser.TryParseNoOperand(operands) ? new RuntimeCall(lineIndex) : null;
        });
    }

}

internal enum SyscallCode {
    PrintInt = 1,
    PrintString = 4,
    ReadInt = 5,
    ReadString = 8,
    Exit = 10,
}
