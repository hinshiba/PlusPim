using PlusPim.Debuggers.PlusPimDbg.Instruction.Instructions.Factories;
using PlusPim.Debuggers.PlusPimDbg.Instruction.Parser;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Globalization;

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
    private readonly Stack<uint> _prevV0 = new();
    private readonly Stack<byte[]> _consumedReadInt = new();
    private readonly Stack<int> _prevReadChar = new();
    private readonly Stack<ReadStringRecord> _prevReadString = new();

    /// <summary>
    /// read_stringのUndo用の情報
    /// </summary>
    /// <param name="Address">書き込み先</param>
    /// <param name="PrevBytes">書き込み前の内容(書き込んだ範囲のみ)</param>
    /// <param name="Consumed">この命令が消費した入力のバイト列(入力バッファへ戻した残りは含まない)</param>
    private readonly record struct ReadStringRecord(Address Address, byte[] PrevBytes, byte[] Consumed);


    public ExecuteResult Execute(RuntimeContext context) {
        if(!context.IsKernelMode) {
            // カーネル空間でないならコプロセッサ例外．機能は実行せず，Undo用の情報も積まない
            return ExecuteResult.Raise(ExcCode.CpU);
        }

        SyscallCode code = (SyscallCode)context.Registers[RegisterID.V0];
        if(!Enum.IsDefined(code)) {
            // 未知の番号はランタイムエラー．入出力も行わず，Undo用の情報も積まない
            return ExecuteResult.Fail(
                RuntimeErrorKind.UnknownRuntimeCall,
                $"unknown runtime call code {context.Registers[RegisterID.V0]} in $v0 (syscall at 0x{context.GetCP0Snapshot().Epc.Addr:X8})"
            );
        }

        switch(code) {
            case SyscallCode.PrintInt:
                context.Log($"RuntimeCall: print_int {context.Registers[RegisterID.A0]}");

                // 符号付き32bit整数として出力する
                Console.Write(((int)context.Registers[RegisterID.A0]).ToString(CultureInfo.InvariantCulture));
                break;

            case SyscallCode.PrintString:
                context.Log($"RuntimeCall: print_string at address 0x{context.Registers[RegisterID.A0]:X8}");

                Address readAddr = new(context.Registers[RegisterID.A0]);
                List<byte> bytes = [];

                for(byte b; (b = context.ReadMemoryByte(readAddr++)) != 0;) {
                    bytes.Add(b);
                }
                Console.Write(System.Text.Encoding.UTF8.GetString([.. bytes]));
                break;

            case SyscallCode.ReadInt:
                context.Log("RuntimeCall: read_int to $v0");

                // Undoのために現在の値を保存
                this._prevV0.Push(context.Registers[RegisterID.V0]);

                // ユーザーからの入力を1行読み，整数として解釈する
                byte[] intLine = context.Input.ReadLine();
                // Undoのために消費した入力を保存
                this._consumedReadInt.Push(intLine);
                string intText = System.Text.Encoding.UTF8.GetString(intLine);
                if(int.TryParse(intText.TrimEnd('\r', '\n'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) {
                    context.Registers[RegisterID.V0] = (uint)value;
                } else {
                    context.Log("RuntimeCall: Invalid input for read_int, so set 0");
                    context.Registers[RegisterID.V0] = 0;
                }
                break;

            case SyscallCode.ReadString:
                context.Log($"RuntimeCall: read_string to address 0x{context.Registers[RegisterID.A0]:X8}");

                Address writeAddr = new(context.Registers[RegisterID.A0]);
                uint maxLength = context.Registers[RegisterID.A1];

                if(maxLength == 0) {
                    // 何も書き込まず，入力も読まない
                    this._prevReadString.Push(new(writeAddr, [], []));
                    break;
                }

                // 改行を含む1行を読む．EOFなら空
                byte[] line = context.Input.ReadLine();

                // 書き込むのは最大 maxLength-1 バイトとNUL．実際の入力長を超えては扱わない
                int written = (int)Math.Min((uint)line.Length, maxLength - 1);

                // 書き込んだバイトまでを消費とし，入りきらなかった分は次の読み取りのために残す
                // 多バイト文字の途中で切れた場合も，残りのバイトは失われず次の読み取りで読まれる
                context.Input.PushFront(line.AsSpan(written));

                // Undoのために書き込む範囲(NUL込み)のメモリの内容を保存
                byte[] prevBytes = new byte[written + 1];
                for(int i = 0; i < prevBytes.Length; i++) {
                    prevBytes[i] = context.ReadMemoryByte(writeAddr + i);
                }
                this._prevReadString.Push(new(writeAddr, prevBytes, line[..written]));

                for(int i = 0; i < written; i++) {
                    context.WriteMemoryByte(writeAddr + i, line[i]);
                }
                context.WriteMemoryByte(writeAddr + written, 0); // null terminator
                break;

            case SyscallCode.PrintChar:
                context.Log($"RuntimeCall: print_char 0x{context.Registers[RegisterID.A0] & 0xFF:X2}");

                // $a0の下位1バイトをそのまま文字として出力する
                Console.Write((char)(context.Registers[RegisterID.A0] & 0xFF));
                break;

            case SyscallCode.ReadChar:
                context.Log("RuntimeCall: read_char to $v0");

                // Undoのために現在の値を保存
                this._prevV0.Push(context.Registers[RegisterID.V0]);

                // UTF-8 の1バイトだけを消費する．EOFなら-1
                int ch = context.Input.ReadChar();
                this._prevReadChar.Push(ch);
                context.Registers[RegisterID.V0] = (uint)ch;
                break;

            case SyscallCode.Exit:
                context.Log("RuntimeCall: exit");
                context.IsTerminated = true;
                break;
        }
        this._history.Push(code);
        return ExecuteResult.Next;
    }
    public void Undo(RuntimeContext context) {
        switch(this._history.Pop()) {
            case SyscallCode.PrintInt:
            case SyscallCode.PrintString:
            case SyscallCode.PrintChar:
                // 画面に出力した内容は消せないので無視
                break;

            case SyscallCode.ReadInt:
                // レジスタの値の復元と，消費した入力の返却
                context.Registers[RegisterID.V0] = this._prevV0.Pop();
                context.Input.PushFront(this._consumedReadInt.Pop());
                break;

            case SyscallCode.ReadChar:
                // レジスタの値の復元と，消費したバイトの返却
                context.Registers[RegisterID.V0] = this._prevV0.Pop();
                int ch = this._prevReadChar.Pop();
                if(ch != PendingInput.Eof) {
                    context.Input.PushFront([(byte)ch]);
                }
                break;

            case SyscallCode.ReadString:
                // メモリの内容の復元
                ReadStringRecord rec = this._prevReadString.Pop();
                Address addr = rec.Address;
                foreach(byte b in rec.PrevBytes) {
                    context.WriteMemoryByte(addr++, b);
                }
                // 消費した入力の返却(後続の命令はUndo済みなので，残りは先頭にある)
                context.Input.PushFront(rec.Consumed);
                break;

            case SyscallCode.Exit:
                // フラグの復元
                context.IsTerminated = false;
                break;
        }
    }

    /// <summary>
    /// 命令のパーサーを生成するファクトリ
    /// </summary>
    internal static Func<string, IInstructionParser> CreateParser() {
        return mnemonic => new FuncInstructionParser(mnemonic, (operands, lineNumber) => {
            return OperandParser.TryParseNoOperand(operands) ? new RuntimeCall(lineNumber) : null;
        });
    }

}

internal enum SyscallCode {
    PrintInt = 1,
    PrintString = 4,
    ReadInt = 5,
    ReadString = 8,
    Exit = 10,
    PrintChar = 11,
    ReadChar = 12,
}
