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
    private readonly Stack<string> _consumedReadInt = new();
    private readonly Stack<int> _prevReadChar = new();
    private readonly Stack<ReadStringRecord> _prevReadString = new();

    /// <summary>
    /// read_stringのUndo用の情報
    /// </summary>
    /// <param name="Address">書き込み先</param>
    /// <param name="PrevBytes">書き込み前の内容(書き込んだ範囲のみ)</param>
    /// <param name="Consumed">この命令が消費した入力(入力バッファへ戻した残りは含まない)</param>
    private readonly record struct ReadStringRecord(Address Address, byte[] PrevBytes, string Consumed);


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
                $"RuntimeError: unknown runtime call code {context.Registers[RegisterID.V0]} in $v0 (syscall at 0x{context.GetCP0Snapshot().Epc.Addr:X8})"
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
                string? intLine = PendingInput.For(context).ReadLine();
                // Undoのために消費した入力を保存
                this._consumedReadInt.Push(intLine ?? "");
                if(int.TryParse(intLine?.TrimEnd('\r', '\n'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) {
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
                PendingInput pending = PendingInput.For(context);

                if(maxLength == 0) {
                    // 何も書き込まず，入力も読まない
                    this._prevReadString.Push(new(writeAddr, [], ""));
                    break;
                }

                // 改行を含む1行を読む．EOFなら空
                string line = pending.ReadLine() ?? "";
                byte[] lineBytes = System.Text.Encoding.UTF8.GetBytes(line);

                // 書き込むのは最大 maxLength-1 バイトとNUL．実際の入力長を超えては扱わない
                int written = (int)Math.Min((uint)lineBytes.Length, maxLength - 1);

                // 全体が書き込める文字までを消費とし，入りきらなかった分は次の読み取りのために残す
                int consumedLength = 0;
                int consumedBytes = 0;
                foreach(System.Text.Rune rune in line.EnumerateRunes()) {
                    consumedBytes += rune.Utf8SequenceLength;
                    if(written < consumedBytes) {
                        break;
                    }
                    consumedLength += rune.Utf16SequenceLength;
                }
                pending.PushFront(line[consumedLength..]);

                // Undoのために書き込む範囲(NUL込み)のメモリの内容を保存
                byte[] prevBytes = new byte[written + 1];
                for(int i = 0; i < prevBytes.Length; i++) {
                    prevBytes[i] = context.ReadMemoryByte(writeAddr + i);
                }
                this._prevReadString.Push(new(writeAddr, prevBytes, line[..consumedLength]));

                for(int i = 0; i < written; i++) {
                    context.WriteMemoryByte(writeAddr + i, lineBytes[i]);
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

                // 1文字だけ消費する．EOFなら-1
                int ch = PendingInput.For(context).ReadChar();
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
                PendingInput.For(context).PushFront(this._consumedReadInt.Pop());
                break;

            case SyscallCode.ReadChar:
                // レジスタの値の復元と，消費した文字の返却
                context.Registers[RegisterID.V0] = this._prevV0.Pop();
                int ch = this._prevReadChar.Pop();
                if(0 <= ch) {
                    PendingInput.For(context).PushFront(((char)ch).ToString());
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
                PendingInput.For(context).PushFront(rec.Consumed);
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
    PrintChar = 11,
    ReadChar = 12,
}

/// <summary>
/// 標準入力のうち，消費されずに残っている文字．read_int/read_char/read_stringで共有する
/// </summary>
/// <remarks>実行(RuntimeContext)ごとに1つ持つ</remarks>
internal sealed class PendingInput {
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RuntimeContext, PendingInput> Table = new();

    private string _pending = "";

    public static PendingInput For(RuntimeContext context) {
        return Table.GetValue(context, _ => new PendingInput());
    }

    /// <summary>
    /// 1文字読む．EOFなら-1
    /// </summary>
    public int ReadChar() {
        if(this._pending.Length == 0) {
            return Console.In.Read();
        }
        char c = this._pending[0];
        this._pending = this._pending[1..];
        return c;
    }

    /// <summary>
    /// 改行を含む1行を読む．何も読めない(EOF)なら <c>null</c>
    /// </summary>
    public string? ReadLine() {
        System.Text.StringBuilder sb = new();
        int c;
        while((c = this.ReadChar()) >= 0) {
            sb.Append((char)c);
            if(c == '\n') {
                break;
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>
    /// 読み取り位置の手前に文字列を戻す
    /// </summary>
    public void PushFront(string text) {
        this._pending = text + this._pending;
    }
}
