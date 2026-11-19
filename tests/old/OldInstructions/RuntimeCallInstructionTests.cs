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
public sealed class RuntimeCallInstructionTests {
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
    /// 命令が読んではならない入力 (ユーザーモード等で入力が消費されないことの確認用)
    /// </summary>
    private const string UntouchedInput = "123\nabc\n";

    private readonly InMemoryDebuggeeIO _io = new();

    /// <summary>
    /// デバッギの入力のうち，まだ読まれていない残りを文字列として返す
    /// </summary>
    private string RemainingInput() {
        return Encoding.UTF8.GetString(this._io.RemainingInput);
    }

    // ---- パラメータ ----

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
    private RuntimeContext CreateKernel(uint code) {
        RuntimeContext context = InstructionHarness.CreateKernel(ExcCode.Sys, InstructionHarness.InstructionAddress, io: this._io);
        FillWindow(context);
        context.Registers[RegisterID.V0] = code;
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

    /// <summary>
    /// read_string が入力バッファへ戻す，書き込まれなかった残り．<c>$a1 = 0</c> なら入力を読まないので空
    /// </summary>
    private static byte[] ExpectedReadStringRemainder(string input, uint a1) {
        if(a1 == 0) {
            return [];
        }
        byte[] inputBytes = Encoding.ASCII.GetBytes(input + "\n");
        int length = (int)Math.Min((uint)inputBytes.Length, a1 - 1);
        return inputBytes[length..];
    }

    // ---- ユーザーモード (異常系: CpU) ----

    // ---- print_int ----

    [Theory]
    [MemberData(nameof(PrintIntCases))]
    public void Execute_PrintInt_PrintsSignedDecimal(uint a0) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(PrintInt);
        context.Registers[RegisterID.A0] = a0;
        this._io.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(ExpectedPrintInt(a0), this._io.Output);
        Assert.Equal(UntouchedInput, this.RemainingInput());
    }

    // ---- print_string ----

    /// <summary>
    /// <paramref name="text"/> の NUL 終端文字列の後ろに NUL でない値を置き，NUL で止まることを確認できるようにする
    /// </summary>
    private RuntimeContext SetupPrintString(string text) {
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
        this._io.SetInput(UntouchedInput);
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(Encoding.UTF8.GetBytes(text), this._io.OutputBytes);
        Assert.Equal(UntouchedInput, this.RemainingInput());
    }

    // ---- read_int ----

    [Theory]
    [MemberData(nameof(ReadIntCases))]
    public void Execute_ReadInt_WritesParsedValueToV0(string line, uint expectedV0) {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        this._io.SetInput($"{line}\nnext\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, expectedV0), Capture(context));
        // 1行だけを読む
        Assert.Equal("next\n", this.RemainingInput());
        Assert.Equal("", this._io.Output);
    }

    // ---- read_string ----

    private RuntimeContext SetupReadString(uint a1) {
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
        this._io.SetInput($"{input}\nnext\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);

        // バッファ以外 (メモリ窓の残りを含む) は変化しない
        MachineState expected = before
            .WithMemory(BufferAddress, ExpectedReadStringBytes(input, a1))
            .WithPendingInput(ExpectedReadStringRemainder(input, a1));
        MachineState.AssertEqual(expected, Capture(context));
        // $a1 = 0 では入力を読まない
        Assert.Equal(a1 == 0 ? $"{input}\nnext\n" : "next\n", this.RemainingInput());
        Assert.Equal("", this._io.Output);
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

        Assert.Equal(expected, this._io.Output);
    }

    [Theory]
    [InlineData(0x00u)]
    [InlineData(0x41u)]
    [InlineData(0x7Fu)]
    [InlineData(0x80u)]
    [InlineData(0xA9u)]
    [InlineData(0xFFu)]
    public void Execute_PrintChar_WritesExactlyTheLowByte(uint low) {
        RuntimeContext context = CreateKernel(PrintChar);
        // 上位ビットは出力に影響しない
        context.Registers[RegisterID.A0] = 0xDEADBE00 | low;

        _ = Processor.Execute(context, Parse());

        Assert.Equal([(byte)low], this._io.OutputBytes);
    }

    [Fact]
    public void Execute_PrintChar_Utf8BytesOneByOneAreWrittenVerbatim() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(PrintChar);

        // "あ" のUTF-8 を1バイトずつ出力する
        foreach(byte b in new byte[] { 0xE3, 0x81, 0x82 }) {
            context.Registers[RegisterID.A0] = b;
            _ = Processor.Execute(context, inst);
        }

        Assert.Equal([0xE3, 0x81, 0x82], this._io.OutputBytes);
        Assert.Equal("あ", this._io.Output);
    }

    [Fact]
    public void Execute_PrintString_WritesInvalidUtf8Verbatim() {
        RuntimeContext context = CreateKernel(PrintString);
        byte[] bytes = [0xFF, 0xFE, 0x61, 0x00, 0x62];
        for(int i = 0; i < bytes.Length; i++) {
            context.WriteMemoryByte(BufferAddress + i, bytes[i]);
        }
        context.Registers[RegisterID.A0] = BufferAddress.Addr;

        _ = Processor.Execute(context, Parse());

        // NUL までをそのまま出力する
        Assert.Equal([0xFF, 0xFE, 0x61], this._io.OutputBytes);
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
        Assert.Equal(expected, this._io.Output);

        Processor.Undo(context, record);
        MachineState.AssertEqual(before, Capture(context));
        Assert.Equal(expected, this._io.Output);
    }

    [Fact]
    public void Execute_ReadChar_MultibyteCharacterArrivesAsSeveralNonNegativeReads() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadChar);
        this._io.SetInput(0xE3, 0x81, 0x82);

        List<uint> values = [];
        for(int i = 0; i < 3; i++) {
            context.Registers[RegisterID.V0] = ReadChar;
            _ = Processor.Execute(context, inst);
            values.Add(context.Registers[RegisterID.V0]);
        }

        Assert.Equal([0xE3u, 0x81u, 0x82u], values);
    }

    [Fact]
    public void Execute_ReadChar_Byte0xFFIsDistinctFromEof() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadChar);
        this._io.SetInput(0xFF);

        _ = Processor.Execute(context, inst);
        Assert.Equal(0x000000FFu, context.Registers[RegisterID.V0]);

        context.Registers[RegisterID.V0] = ReadChar;
        _ = Processor.Execute(context, inst);
        Assert.Equal(0xFFFFFFFFu, context.Registers[RegisterID.V0]);
    }

    [Fact]
    public void Execute_ReadString_RemainderIsReadByReadInt() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(3);
        this._io.SetInput("1234\n");

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
        this._io.SetInput("hi\n");

        _ = Processor.Execute(context, inst);

        AssertBytes(context, BufferAddress, (byte)'h', (byte)'i', (byte)'\n', 0);
    }

    [Fact]
    public void Execute_ReadString_ZeroWritesNothingAndOneWritesNulOnly() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(0);
        this._io.SetInput("abc\n");
        MachineState before = Capture(context);

        _ = Processor.Execute(context, inst);
        MachineState.AssertEqual(before, Capture(context));

        context.Registers[RegisterID.A1] = 1;
        _ = Processor.Execute(context, inst);
        // 1行を読んで何も書けないので，行の全体が入力バッファに残る
        MachineState.AssertEqual(
            before.WithRegister(RegisterID.A1, 1).WithMemory(BufferAddress, [0]).WithPendingInput((byte)'a', (byte)'b', (byte)'c', (byte)'\n'),
            Capture(context)
        );
    }

    [Fact]
    public void Execute_ReadString_HugeLengthWritesOnlyActualLine() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(0xffffffff);
        this._io.SetInput("ab\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithMemory(BufferAddress, [(byte)'a', (byte)'b', (byte)'\n', 0]), Capture(context));

        Processor.Undo(context, record);
        MachineState.AssertEqual(before.WithPendingInput((byte)'a', (byte)'b', (byte)'\n'), Capture(context));
    }

    [Fact]
    public void Execute_ReadString_TruncatesByBytesAndMayCutAMultibyteCharacter() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(5);
        this._io.SetInput("あい\nnext\n");
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        // 4バイト + NUL．"い" (E3 81 84) は先頭の1バイトだけが書かれ，残りは入力バッファに残る
        MachineState expected = before
            .WithMemory(BufferAddress, 0xE3, 0x81, 0x82, 0xE3, 0x00)
            .WithPendingInput(0x81, 0x84, 0x0A);
        MachineState.AssertEqual(expected, Capture(context));
        Assert.Equal("next\n", this.RemainingInput());

        Processor.Undo(context, record);
        MachineState.AssertEqual(before.WithPendingInput(Encoding.UTF8.GetBytes("あい\n")), Capture(context));
        Assert.Equal("next\n", this.RemainingInput());
    }

    [Fact]
    public void Execute_ReadString_StoresInvalidUtf8Verbatim() {
        IInstruction inst = Parse();
        RuntimeContext context = SetupReadString(100);
        this._io.SetInput(0xFF, 0xFE, 0x0A, 0x61);

        _ = Processor.Execute(context, inst);

        AssertBytes(context, BufferAddress, 0xFF, 0xFE, 0x0A, 0x00);
        Assert.Equal([0x61], this._io.RemainingInput);
    }

    [Fact]
    public void ReadInt_NonAsciiLine_GivesZeroAndConsumesTheLine() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        // 全角数字の行はASCIIの整数として解釈できない
        this._io.SetInput(Encoding.UTF8.GetBytes("\uFF14\uFF12\nnext\n"));
        MachineState before = Capture(context);

        ExecutionRecord record = Processor.Execute(context, inst);

        MachineState.AssertEqual(before.WithRegister(RegisterID.V0, 0), Capture(context));
        Assert.Equal("next\n", this.RemainingInput());

        Processor.Undo(context, record);
        MachineState.AssertEqual(before.WithPendingInput(Encoding.UTF8.GetBytes("\uFF14\uFF12\n")), Capture(context));
    }

    [Fact]
    public void ReadInt_InvalidUtf8Line_GivesZero() {
        IInstruction inst = Parse();
        RuntimeContext context = CreateKernel(ReadInt);
        this._io.SetInput(0x31, 0xFF, 0x0A);

        _ = Processor.Execute(context, inst);

        Assert.Equal(0u, context.Registers[RegisterID.V0]);
        Assert.Empty(this._io.RemainingInput);
    }

    /// <summary>
    /// 未消費の入力(入力バッファとデバッギの入力の残り)の全体．状態は変えない
    /// </summary>
    private string PeekRemainingInput(RuntimeContext context) {
        return Encoding.UTF8.GetString([.. context.PendingInput.ToArray(), .. this._io.RemainingInput]);
    }

    // ---- exit ----

    // ---- 未知の番号 ----

    // ---- パラメータと繰り返し実行 ----

}
