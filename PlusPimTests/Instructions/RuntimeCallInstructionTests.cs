using PlusPim.Debuggers.PlusPimDbg.Instruction;
using PlusPim.Debuggers.PlusPimDbg.Program.Records;
using PlusPim.Debuggers.PlusPimDbg.Runtime;
using System.Globalization;
using System.Text;
using Xunit;

namespace PlusPimTests.Instructions;

/// <summary>
/// <c>runtime_call!</c> の命令レベルテスト (doc/tests/instructions/runtime_call_model.md)
/// </summary>
/// <remarks>オペランドを持たない命令なので，レジスタエイリアスパターンは適用しない</remarks>
[Collection(ConsoleCollection.Name)]
public sealed class RuntimeCallInstructionTests: IDisposable {
    /// <summary>
    /// ユーザーモードの CP0 の状態 (cp0_model.md「CP0 の状態」)
    /// </summary>
    public enum UserModeState {
        /// <summary>初期値</summary>
        Initial,
        /// <summary><c>eret</c> 後 (Cause/EPC/BadVAddr に値が残っている)</summary>
        PostEret,
    }

    private const uint PrintInt = 1;
    private const uint PrintString = 4;
    private const uint ReadInt = 5;
    private const uint ReadString = 8;
    private const uint Exit = 10;
    private const uint PrintChar = 11;
    private const uint ReadChar = 12;

    /// <summary>
    /// print_string/read_string が使うバッファ
    /// </summary>
    private static readonly Address BufferAddress = new(0x10010000);

    /// <summary>
    /// 検証するメモリ窓．バッファの前後を含む
    /// </summary>
    private static readonly Address WindowBase = new(0x10010000 - 8);
    private const int WindowSize = 64;

    /// <summary>
    /// eret 後のコンテキストに残す例外の情報
    /// </summary>
    private static readonly Address PostEretEpc = new(0x00400040);
    private const uint PostEretBadVAddr = 0x10000001;

    /// <summary>
    /// 命令が読んではならない入力 (ユーザーモード等で入力が消費されないことの確認用)
    /// </summary>
    private const string UntouchedInput = "123\nabc\n";

    private readonly ConsoleRedirect _console = new();

    public void Dispose() {
        this._console.Dispose();
    }

    // ---- パラメータ ----

    /// <summary>
    /// すべての機能番号 (未知の番号を含む)
    /// </summary>
    private static readonly uint[] AllCodes = [PrintInt, PrintString, ReadInt, ReadString, Exit, PrintChar, ReadChar, 0, 13];

    public static TheoryData<UserModeState, uint> UserModeCases() {
        TheoryData<UserModeState, uint> data = new();
        foreach(UserModeState state in Enum.GetValues<UserModeState>()) {
            foreach(uint code in AllCodes) {
                data.Add(state, code);
            }
        }
        return data;
    }

    public static TheoryData<uint> PrintIntCases() {
        return TestValues.Cases(TestValues.Signed32);
    }

    public static TheoryData<string> PrintStringCases() {
        return new TheoryData<string> {
            "",
            "Hello, MIPS!",
            "こんにちは, 世界",
            "naïve 🎉",
        };
    }

    /// <summary>
    /// (入力行, 期待する <c>$v0</c>)．期待値は仕様から手で求めた値
    /// </summary>
    public static TheoryData<string, uint> ReadIntCases() {
        return new TheoryData<string, uint> {
            // 正の数・負の数
            { "0", 0x00000000 },
            { "42", 0x0000002a },
            { "-42", 0xffffffd6 },
            // INT_MAX/INT_MIN
            { "2147483647", 0x7fffffff },
            { "-2147483648", 0x80000000 },
            // 範囲外の数
            { "2147483648", 0x00000000 },
            { "-2147483649", 0x00000000 },
            { "99999999999", 0x00000000 },
            // 数値でない文字列
            { "abc", 0x00000000 },
            // 空行
            { "", 0x00000000 },
        };
    }

    /// <summary>
    /// (入力行, <c>$a1</c>)．入力は改行を含まない1行の ASCII 文字列(読み取り結果には改行が付く)
    /// </summary>
    public static TheoryData<string, uint> ReadStringCases() {
        const string input = "PlusPim"; // 7 バイト
        const string longInput = "abcdefghijklmnopqrst"; // 20 バイト
        return new TheoryData<string, uint> {
            { input, 0 },
            { input, 1 },
            { input, 4 }, // 入力長より小さい
            { input, 7 }, // 入力長と等しい
            { input, 8 }, // 入力長 + 1 (入力全体と NUL がちょうど入る)
            { input, 12 }, // 入力長より大きい
            { longInput, 5 }, // 入力が制限より長い
            { "", 0 },
            { "", 1 },
            { "", 4 }, // 入力が空
        };
    }

    public static TheoryData<uint> UnknownCodeCases() {
        return new TheoryData<uint> { 0, 13 };
    }

    // ---- 準備 ----

    private static IInstruction Parse() {
        return InstructionHarness.Parse("runtime_call!");
    }

    /// <summary>
    /// メモリ窓を NUL でも ASCII でもない値で埋める (書き込まれた範囲と NUL を見分けるため)
    /// </summary>
    private static void FillWindow(RuntimeContext context) {
        for(int i = 0; i < WindowSize; i++) {
            context.WriteMemoryByte(WindowBase + i, (byte)(0xc0 + (i % 16)));
        }
    }

    /// <summary>
    /// 例外ハンドラ実行中 (syscall 直後) のカーネルモードのコンテキスト
    /// </summary>
    private static RuntimeContext CreateKernel(uint code) {
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Sys, InstructionHarness.InstructionAddress);
        FillWindow(context);
        context.Registers[RegisterID.V0] = code;
        return context;
    }

    private static RuntimeContext CreateUser(UserModeState state, uint code) {
        RuntimeContext context = state switch {
            UserModeState.Initial => InstructionHarness.CreateUser(),
            UserModeState.PostEret => InstructionHarness.CreatePostEret(ExcCode.AdEL, PostEretEpc, PostEretBadVAddr),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
        FillWindow(context);
        // どの機能でも効果が観測できるように引数を用意する
        WriteCString(context, BufferAddress, "abc");
        context.Registers[RegisterID.V0] = code;
        context.Registers[RegisterID.A0] = BufferAddress.Addr;
        context.Registers[RegisterID.A1] = 8;
        return context;
    }

    private static byte[] CString(string text) {
        return [.. Encoding.UTF8.GetBytes(text), 0];
    }

    private static void WriteCString(RuntimeContext context, Address address, string text) {
        byte[] bytes = CString(text);
        for(int i = 0; i < bytes.Length; i++) {
            context.WriteMemoryByte(address + i, bytes[i]);
        }
    }

    private static MachineState Capture(RuntimeContext context) {
        return MachineState.Capture(context, WindowBase, WindowSize);
    }

    // ---- 期待値の計算 (実装に依存しない) ----

    private static string ExpectedPrintInt(uint value) {
        return ((int)value).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// read_string が書き込むバイト列．<c>$a1 = 0</c> なら空
    /// </summary>
    private static byte[] ExpectedReadStringBytes(string input, uint a1) {
        if(a1 == 0) {
            return [];
        }
        byte[] inputBytes = Encoding.ASCII.GetBytes(input + "\n"); // 改行を含む
        int length = (int)Math.Min((uint)inputBytes.Length, a1 - 1);
        return [.. inputBytes[..length], 0];
    }

    // ---- ユーザーモード (異常系: CpU) ----

    [Theory]
    [MemberData(nameof(UserModeCases))]
    public void Execute_UserMode_RaisesCpU(UserModeState state, uint code) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateUser(state, code);
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        // 機能は実行されず，CP0 だけが例外発生直後の状態になる
        MachineState expected = before.WithException(ExcCode.CpU, InstructionHarness.InstructionAddress);
        MachineState.AssertEqual(expected, Capture(context));
        Assert.True(context.IsKernelMode);
        Assert.Equal("", this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    [Theory]
    [MemberData(nameof(UserModeCases))]
    public void Undo_UserMode_RestoresState(UserModeState state, uint code) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateUser(state, code);
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        Assert.False(context.IsKernelMode);
        Assert.Equal("", this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    // ---- print_int ----

    [Theory]
    [MemberData(nameof(PrintIntCases))]
    public void Execute_PrintInt_PrintsSignedDecimal(uint a0) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(PrintInt);
        context.Registers[RegisterID.A0] = a0;
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(ExpectedPrintInt(a0), this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    [Theory]
    [MemberData(nameof(PrintIntCases))]
    public void Undo_PrintInt_RestoresState(uint a0) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(PrintInt);
        context.Registers[RegisterID.A0] = a0;
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        string printed = this._console.Output;
        Processor.Undo(context, record);

        // undo は状態を変えず，何も出力しない
        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(printed, this._console.Output);
    }

    // ---- print_string ----

    /// <summary>
    /// <paramref name="text"/> の NUL 終端文字列の後ろに NUL でない値を置き，NUL で止まることを確認できるようにする
    /// </summary>
    private static RuntimeContext SetupPrintString(string text) {
        RuntimeContext context = CreateKernel(PrintString);
        WriteCString(context, BufferAddress, text);
        Address guard = BufferAddress + CString(text).Length;
        foreach(byte b in "XYZ"u8) {
            context.WriteMemoryByte(guard, b);
            guard++;
        }
        context.Registers[RegisterID.A0] = BufferAddress.Addr;
        return context;
    }

    [Theory]
    [MemberData(nameof(PrintStringCases))]
    public void Execute_PrintString_PrintsUntilNul(string text) {
        IInstruction inst = Parse();
        RuntimeContext context = SetupPrintString(text);
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(text, this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    [Theory]
    [MemberData(nameof(PrintStringCases))]
    public void Undo_PrintString_RestoresState(string text) {
        IInstruction inst = Parse();
        RuntimeContext context = SetupPrintString(text);
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        string printed = this._console.Output;
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(printed, this._console.Output);
    }

    // ---- read_int ----

    [Theory]
    [MemberData(nameof(ReadIntCases))]
    public void Execute_ReadInt_WritesParsedValueToV0(string line, uint expectedV0) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        this._console.SetInput($"{line}\nnext\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, expectedV0), Capture(context));
        // 1行だけを読む
        Assert.Equal("next\n", this._console.ReadRemainingInput());
        Assert.Equal("", this._console.Output);
    }

    [Theory]
    [MemberData(nameof(ReadIntCases))]
    public void Undo_ReadInt_RestoresState(string line, uint expectedV0) {
        _ = expectedV0;
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        this._console.SetInput($"{line}\nnext\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        // undo は入力を読まない
        Assert.Equal("next\n", this._console.ReadRemainingInput());
        Assert.Equal("", this._console.Output);
    }

    // ---- read_string ----

    private static RuntimeContext SetupReadString(uint a1) {
        RuntimeContext context = CreateKernel(ReadString);
        context.Registers[RegisterID.A0] = BufferAddress.Addr;
        context.Registers[RegisterID.A1] = a1;
        return context;
    }

    [Theory]
    [MemberData(nameof(ReadStringCases))]
    public void Execute_ReadString_WritesAtMostA1MinusOneBytesAndNul(string input, uint a1) {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(a1);
        this._console.SetInput($"{input}\nnext\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        // バッファ以外 (メモリ窓の残りを含む) は変化しない
        MachineState expected = before.WithMemory(BufferAddress, ExpectedReadStringBytes(input, a1));
        MachineState.AssertEqual(expected, Capture(context));
        // $a1 = 0 では入力を読まない
        Assert.Equal(a1 == 0 ? $"{input}\nnext\n" : "next\n", this._console.ReadRemainingInput());
        Assert.Equal("", this._console.Output);
    }

    [Theory]
    [MemberData(nameof(ReadStringCases))]
    public void Undo_ReadString_RestoresState(string input, uint a1) {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(a1);
        this._console.SetInput($"{input}\nnext\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(a1 == 0 ? $"{input}\nnext\n" : "next\n", this._console.ReadRemainingInput());
        Assert.Equal("", this._console.Output);
    }

    // ---- print_char / read_char / read_string の追加仕様 ----

    private static void AssertBytes(RuntimeContext context, Address address, params byte[] expected) {
        for(int i = 0; i < expected.Length; i++) {
            Assert.Equal(expected[i], context.ReadMemoryByte(address + i));
        }
    }

    [Theory]
    [InlineData(0xffffffffu, "-1")]
    [InlineData(0x80000000u, "-2147483648")]
    [InlineData(0xfffffffeu, "-2")]
    public void Execute_PrintInt_NegativeIsSigned(uint a0, string expected) {
        RuntimeContext context = CreateKernel(PrintInt);
        context.Registers[RegisterID.A0] = a0;

        _ = Processor.Execute(context, Parse());

        Assert.Equal(expected, this._console.Output);
    }

    [Theory]
    [InlineData(0x00000041u, "A")]
    [InlineData(0xdeadbe41u, "A")] // 上位ビットは無視する
    [InlineData(0x00000141u, "A")]
    public void Execute_PrintChar_WritesLowByte(uint a0, string expected) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(PrintChar);
        context.Registers[RegisterID.A0] = a0;
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(expected, this._console.Output);

        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(expected, this._console.Output);
    }

    [Fact]
    public void Execute_ReadChar_ConsumesOneCharIntoV0() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadChar);
        this._console.SetInput("xyz\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, 'x'), Capture(context));
        Assert.Equal("yz\n", this._console.ReadRemainingInput());
    }

    [Fact]
    public void Execute_ReadChar_EofGivesMinusOne() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadChar);
        this._console.SetInput("");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, 0xffffffff), Capture(context));
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));
    }

    [Fact]
    public void Undo_ReadChar_RestoresV0AndInput() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadChar);
        this._console.SetInput("xyz\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));

        // 再実行すると同じ文字が読める
        _ = Processor.Execute(context, inst);
        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, 'x'), Capture(context));
    }

    [Fact]
    public void Execute_ReadString_IncludesNewlineAndLeavesRemainder() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(4);
        this._console.SetInput("abcdef\nnext\n");

        _ = Processor.Execute(context, inst);

        // 3バイト + NUL．残り "def\n" は次の読み取りで読める
        AssertBytes(context, BufferAddress, (byte)'a', (byte)'b', (byte)'c', 0);
        Assert.Equal("next\n", this._console.ReadRemainingInput());

        context.Registers[RegisterID.A1] = 100;
        _ = Processor.Execute(context, inst);
        AssertBytes(context, BufferAddress, (byte)'d', (byte)'e', (byte)'f', (byte)'\n', 0);
    }

    [Fact]
    public void Execute_ReadString_RemainderIsReadByReadInt() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(3);
        this._console.SetInput("1234\n");

        _ = Processor.Execute(context, inst);
        AssertBytes(context, BufferAddress, (byte)'1', (byte)'2', 0);

        context.Registers[RegisterID.V0] = ReadInt;
        _ = Processor.Execute(context, inst);
        Assert.Equal(34u, context.Registers[RegisterID.V0]);
    }

    [Fact]
    public void Execute_ReadString_ShortInputIncludesNewline() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(100);
        this._console.SetInput("hi\n");

        _ = Processor.Execute(context, inst);

        AssertBytes(context, BufferAddress, (byte)'h', (byte)'i', (byte)'\n', 0);
    }

    [Fact]
    public void Execute_ReadString_ZeroWritesNothingAndOneWritesNulOnly() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(0);
        this._console.SetInput("abc\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);
        MachineState.AssertEqual(before, Capture(context));

        context.Registers[RegisterID.A1] = 1;
        _ = Processor.Execute(context, inst);
        MachineState.AssertEqual(before.WithRegister(RegisterID.A1, 1).WithMemory(BufferAddress, [0]), Capture(context));
    }

    [Fact]
    public void Execute_ReadString_HugeLengthWritesOnlyActualLine() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(0xffffffff);
        this._console.SetInput("ab\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithMemory(BufferAddress, [(byte)'a', (byte)'b', (byte)'\n', 0]), Capture(context));

        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));
    }

    [Fact]
    public void Undo_ReadString_RestoresBytesAndInput() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(3);
        this._console.SetInput("abcd\nnext\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));

        // 再実行すると同じ入力が読める
        _ = Processor.Execute(context, inst);
        AssertBytes(context, BufferAddress, (byte)'a', (byte)'b', 0);
    }

    /// <summary>
    /// 未消費の入力(PendingInput と標準入力の残り)をすべて取り出して，同じ状態に戻す
    /// </summary>
    private static string PeekRemainingInput(RuntimeContext context) {
        PendingInput pending = PendingInput.For(context);
        StringBuilder sb = new();
        for(int c; (c = pending.ReadChar()) >= 0;) {
            sb.Append((char)c);
        }
        pending.PushFront(sb.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// read_string(3) -> read_int -> (read_string(3) | read_char) を全部 undo して再実行しても，
    /// 同じメモリ・レジスタ・出力になり，同じ入力を消費する
    /// </summary>
    [Theory]
    [InlineData(ReadString)]
    [InlineData(ReadChar)]
    public void UndoAll_ReadStringReadIntThird_ReExecuteIsIdentical(uint thirdCode) {
        const string input = "12345\nabc\nnext\n";
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(3);
        this._console.SetInput(input);
        MachineState initial = Capture(context);

        List<(MachineState State, string Remaining)> RunAll(List<ExecutionRecord> records) {
            List<(MachineState, string)> results = [];
            foreach(uint code in new[] { ReadString, ReadInt, thirdCode }) {
                context.Registers[RegisterID.V0] = code;
                records.Add(Processor.Execute(context, inst));
                results.Add((Capture(context), PeekRemainingInput(context)));
            }
            return results;
        }

        List<ExecutionRecord> firstRecords = [];
        List<(MachineState State, string Remaining)> first = RunAll(firstRecords);
        string firstOutput = this._console.Output;
        // 1回目: read_string が "12" と残り "345\n"，read_int が 345 を消費する
        Assert.Equal("abc\nnext\n", first[1].Remaining);

        for(int i = firstRecords.Count - 1; i >= 0; i--) {
            Processor.Undo(context, firstRecords[i]);
        }
        // $v0 への機能番号の設定はテストが手で行うもので，undo の対象ではない
        context.Registers[RegisterID.V0] = ReadString;
        MachineState.AssertEqual(initial, Capture(context));
        Assert.Equal(input, PeekRemainingInput(context));

        List<(MachineState State, string Remaining)> second = RunAll([]);

        Assert.Equal(first.Count, second.Count);
        for(int i = 0; i < first.Count; i++) {
            MachineState.AssertEqual(first[i].State, second[i].State);
            Assert.Equal(first[i].Remaining, second[i].Remaining);
        }
        Assert.Equal(firstOutput, this._console.Output);
    }

    // ---- exit ----

    [Fact]
    public void Execute_Exit_Terminates() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(Exit);
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithTerminated(), Capture(context));
        Assert.Equal("", this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    [Fact]
    public void Undo_Exit_RestoresState() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(Exit);
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        Assert.False(context.IsTerminated);
    }

    // ---- 未知の番号 ----

    [Theory]
    [MemberData(nameof(UnknownCodeCases))]
    public void Execute_UnknownCode_RaisesRuntimeError(uint code) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(code);
        this._console.SetInput(UntouchedInput);

        RuntimeError error = RuntimeErrorAssert.Raised(() => Processor.Execute(context, inst));

        Assert.Equal(RuntimeErrorKind.UnknownRuntimeCall, error.Kind);
    }

    [Theory]
    [MemberData(nameof(UnknownCodeCases))]
    public void Execute_UnknownCode_NoIOAndChangesOnlyRuntimeError(uint code) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(code);
        this._console.SetInput(UntouchedInput);

        // CP0 も IsTerminated も変化せず，二重例外にもならない
        _ = RuntimeErrorAssert.RaisedWithoutSideEffects(
            context, () => Processor.Execute(context, inst), RuntimeErrorKind.UnknownRuntimeCall, WindowBase, WindowSize
        );
        Assert.Equal("", this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    [Theory]
    [MemberData(nameof(UnknownCodeCases))]
    public void Undo_UnknownCode_RestoresState(uint code) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(code);
        this._console.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);
        Processor.Undo(context, record);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Null(context.RuntimeError);
        Assert.Equal("", this._console.Output);
        Assert.Equal(UntouchedInput, this._console.ReadRemainingInput());
    }

    // ---- パラメータと繰り返し実行 ----

    [Fact]
    public void Parameters_CoverAllCodesAndArguments() {
        Assert.NotEmpty(UserModeCases());
        Assert.NotEmpty(PrintIntCases());
        Assert.NotEmpty(PrintStringCases());
        Assert.NotEmpty(ReadIntCases());
        Assert.NotEmpty(ReadStringCases());
        Assert.NotEmpty(UnknownCodeCases());
        // read_string の $a1 は 0，1，入力長未満，入力長と等しい，入力長超過をすべて含む
        List<(string Input, uint A1)> readString = [.. ReadStringCases().Select(row => ((string)row[0], (uint)row[1]))];
        Assert.Contains(readString, c => c.A1 == 0);
        Assert.Contains(readString, c => c.A1 == 1);
        Assert.Contains(readString, c => c.A1 > 1 && c.A1 < c.Input.Length);
        Assert.Contains(readString, c => c.A1 == c.Input.Length && c.A1 > 1);
        Assert.Contains(readString, c => c.A1 > c.Input.Length + 1);
    }

    [Fact]
    public void RepeatedExecute_ReadIntCpUReadInt_UndoesInReverseOrder() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        this._console.SetInput("17\n-3\n");

        IReadOnlyList<(MachineState BeforeExecute, MachineState AfterExecute)> results = RepeatedExecution.RunWithMemory(
            inst, context, WindowBase, WindowSize,
            // 正常系 (カーネルモードで read_int)
            RepeatedExecution.SetRegisters((RegisterID.V0, ReadInt)),
            // 異常系 (ユーザーモードに戻ってから呼ぶので CpU)
            ctx => {
                ctx.WriteCP0Register(12, 0);
                ctx.PC = InstructionHarness.InstructionAddress;
                ctx.Registers[RegisterID.V0] = ReadInt;
            },
            // 正常系 (CpU の例外ハンドラ内で read_int)
            ctx => {
                ctx.PC = InstructionHarness.KernelInstructionAddress;
                ctx.Registers[RegisterID.V0] = ReadInt;
            }
        );

        MachineState.AssertEqual(results[0].BeforeExecute.WithRegister(RegisterID.V0, 17), results[0].AfterExecute);
        MachineState.AssertEqual(
            results[1].BeforeExecute.WithException(ExcCode.CpU, InstructionHarness.InstructionAddress),
            results[1].AfterExecute
        );
        MachineState.AssertEqual(results[2].BeforeExecute.WithRegister(RegisterID.V0, 0xfffffffd), results[2].AfterExecute);
        // CpU の実行は入力を消費しないので，2行がちょうど読まれる
        Assert.Equal("", this._console.ReadRemainingInput());
        Assert.Equal("", this._console.Output);
    }
}
