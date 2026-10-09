using System.Text.Json;
using Xunit;

namespace PlusPimTests;

/// <summary>
/// <see cref="PlusPim.EditorController.DebugAdapter.DebugAdapter"/> を DAP のメッセージで操作するテスト
/// </summary>
public class DebugAdapterTests {
    /// <summary>
    /// 4行目の <c>addiu $t0, $zero, 2</c> にブレークポイントを置ける短いプログラム
    /// </summary>
    private const string Straight = """
        .text
        main:
          addiu $t0, $zero, 1
          addiu $t0, $zero, 2
          addiu $t0, $zero, 3
        """;

    private static void WithClient(string asm, Action<DapClient, FileInfo> test) {
        FileInfo file = TestHelpers.WriteTempAsm(asm);
        try {
            using DapClient client = new([file]);
            test(client, file);
        } finally {
            file.Delete();
        }
    }

    /// <summary>
    /// initialize から launch までを行い，initialized イベントを待つ
    /// </summary>
    internal static void Launch(DapClient client, bool? stopOnEntry = null) {
        _ = client.RequestOk("initialize", new { adapterID = "pluspim" });
        int mark = client.Mark;
        _ = client.RequestOk("launch", stopOnEntry is bool value ? new { stopOnEntry = value } : (object)new { });
        _ = client.WaitForEvent("initialized", mark);
    }

    /// <summary>
    /// launch から configurationDone までを行い，entry で停止したことを確かめる
    /// </summary>
    internal static void LaunchAndStopOnEntry(DapClient client) {
        Launch(client);
        int mark = client.Mark;
        _ = client.RequestOk("configurationDone");
        JsonElement stopped = client.WaitForEvent("stopped", mark);
        Assert.Equal("entry", stopped.GetProperty("body").GetProperty("reason").GetString());
    }

    internal static object Breakpoints(FileInfo file, params int[] lines) {
        return new { source = new { path = file.FullName }, breakpoints = lines.Select(line => new { line }).ToArray() };
    }

    [Fact]
    public void Initialize_AdvertisesConfigurationDone() {
        WithClient(Straight, (client, _) => {
            JsonElement response = client.RequestOk("initialize", new { adapterID = "pluspim" });
            Assert.True(response.GetProperty("body").GetProperty("supportsConfigurationDoneRequest").GetBoolean());
        });
    }

    [Fact]
    public void InitSequence_FollowsDapOrder() {
        WithClient(Straight, (client, file) => {
            _ = client.RequestOk("initialize", new { adapterID = "pluspim" });

            int launchSeq = client.Send("launch", new { });
            (_, int launchIndex) = client.WaitFor(m => DapClient.IsResponseTo(m, launchSeq));
            (_, int initializedIndex) = client.WaitFor(m => DapClient.IsEvent(m, "initialized"));
            Assert.True(launchIndex < initializedIndex, "initialized must follow the launch response");

            // 構成要求
            JsonElement breakpoints = client.RequestOk("setBreakpoints", Breakpoints(file, 4));
            Assert.True(breakpoints.GetProperty("body").GetProperty("breakpoints")[0].GetProperty("verified").GetBoolean());
            _ = client.RequestOk("setExceptionBreakpoints", new { filters = new[] { "double", "fatal" } });

            int doneSeq = client.Send("configurationDone");
            (_, int doneIndex) = client.WaitFor(m => DapClient.IsResponseTo(m, doneSeq));
            (JsonElement stopped, int stoppedIndex) = client.WaitFor(m => DapClient.IsEvent(m, "stopped"));
            Assert.True(doneIndex < stoppedIndex, "stopped(entry) must follow the configurationDone response");
            Assert.Equal("entry", stopped.GetProperty("body").GetProperty("reason").GetString());

            // configurationDone より前に停止イベントは送られない
            Assert.DoesNotContain(client.Messages[..doneIndex], m => DapClient.IsEvent(m, "stopped"));
        });
    }

    [Fact]
    public void StopOnEntryFalse_RunsAfterConfigurationDone() {
        WithClient(Straight, (client, file) => {
            Launch(client, stopOnEntry: false);
            _ = client.RequestOk("setBreakpoints", Breakpoints(file, 4));

            int mark = client.Mark;
            _ = client.RequestOk("configurationDone");
            JsonElement stopped = client.WaitForEvent("stopped", mark);
            Assert.Equal("breakpoint", stopped.GetProperty("body").GetProperty("reason").GetString());

            JsonElement stackTrace = client.RequestOk("stackTrace", new { threadId = 1 });
            Assert.Equal(4, stackTrace.GetProperty("body").GetProperty("stackFrames")[0].GetProperty("line").GetInt32());
        });
    }

    [Fact]
    public void Launch_FailsWithAssemblyErrors() {
        const string undefinedLabel = """
            .text
            main:
              la $t0, missing
            """;
        WithClient(undefinedLabel, (client, file) => {
            _ = client.RequestOk("initialize", new { adapterID = "pluspim" });
            JsonElement launch = client.Request("launch", new { });
            Assert.False(launch.GetProperty("success").GetBoolean());
            string message = launch.GetProperty("message").GetString()!;
            Assert.Contains("Failed to load program", message);
            Assert.Contains($"{file.Name}:3 Undefined label 'missing'", message);

            // 読み込みに失敗したら initialized を送らない (以降の要求の応答より前に届いていない)
            _ = client.RequestOk("threads");
            Assert.DoesNotContain(client.Messages, m => DapClient.IsEvent(m, "initialized"));
        });
    }

    [Fact]
    public void Disconnect_RespondsBeforeSessionEnds() {
        WithClient(Straight, (client, file) => {
            LaunchAndStopOnEntry(client);

            // Main と同様に，セッション終了と同時に (同じスレッドで) 出力を閉じる
            _ = client.Adapter.WaitForSessionEnd().ContinueWith(task => client.AdapterOutput.Dispose(), TaskContinuationOptions.ExecuteSynchronously);

            JsonElement response = client.Request("disconnect", new { });
            Assert.True(response.GetProperty("success").GetBoolean());
            Assert.True(client.WaitForSessionEnd());
        });
    }

    internal static string StoppedReason(JsonElement stopped) {
        return stopped.GetProperty("body").GetProperty("reason").GetString()!;
    }

    internal static int TopLine(DapClient client) {
        JsonElement stackTrace = client.RequestOk("stackTrace", new { threadId = 1 });
        return stackTrace.GetProperty("body").GetProperty("stackFrames")[0].GetProperty("line").GetInt32();
    }

    /// <summary>
    /// ライブフレームの Registers スコープの変数を名前から値への辞書にする
    /// </summary>
    internal static Dictionary<string, JsonElement> Registers(DapClient client) {
        JsonElement scopes = client.RequestOk("scopes", new { frameId = 1 });
        int reference = scopes.GetProperty("body").GetProperty("scopes")[0].GetProperty("variablesReference").GetInt32();
        JsonElement variables = client.RequestOk("variables", new { variablesReference = reference });
        return variables.GetProperty("body").GetProperty("variables").EnumerateArray()
            .ToDictionary(v => v.GetProperty("name").GetString()!, v => v);
    }

    internal static uint RegisterValue(DapClient client, string name) {
        string value = Registers(client)[name].GetProperty("value").GetString()!;
        return Convert.ToUInt32(value, 16);
    }

    [Fact]
    public void Pause_StopsInfiniteLoop() {
        WithClient(ApplicationExecutionTests.InfiniteLoop, (client, file) => {
            LaunchAndStopOnEntry(client);

            int mark = client.Mark;
            JsonElement cont = client.RequestOk("continue", new { threadId = 1 });
            Assert.True(cont.GetProperty("body").GetProperty("allThreadsContinued").GetBoolean());

            // 実行中も応答する
            _ = client.RequestOk("threads");
            _ = client.RequestOk("readMemory", new { memoryReference = "0x10000000", count = 4 });

            // 実行中の実行の要求は待たせずにエラーにする
            foreach(string command in new[] { "next", "stepIn", "stepOut", "stepBack", "reverseContinue", "continue" }) {
                JsonElement rejected = client.Request(command, new { threadId = 1 });
                Assert.False(rejected.GetProperty("success").GetBoolean(), command);
                Assert.Contains("running", rejected.GetProperty("message").GetString());
            }
            Assert.DoesNotContain(client.Messages[mark..], m => DapClient.IsEvent(m, "stopped"));

            _ = client.RequestOk("pause", new { threadId = 1 });
            JsonElement stopped = client.WaitForEvent("stopped", mark);
            Assert.Equal("pause", StoppedReason(stopped));

            // ループの中で止まり，ループが回っている
            Assert.InRange(TopLine(client), 5, 6);
            Assert.True(0 < RegisterValue(client, "$t0 ($8)"));

            // 止めた後もステップ実行できる
            mark = client.Mark;
            _ = client.RequestOk("stepIn", new { threadId = 1 });
            Assert.Equal("step", StoppedReason(client.WaitForEvent("stopped", mark)));
        });
    }

    [Fact]
    public void Pause_WhileStopped_IsNoOp() {
        WithClient(Straight, (client, file) => {
            LaunchAndStopOnEntry(client);

            int mark = client.Mark;
            _ = client.RequestOk("pause", new { threadId = 1 });
            // 後続の要求の応答までに停止イベントは届かない
            _ = client.RequestOk("threads");
            Assert.DoesNotContain(client.Messages[mark..], m => DapClient.IsEvent(m, "stopped"));
        });
    }

    [Fact]
    public void StepBack_AtHistoryStart_ReportsEntry() {
        WithClient(Straight, (client, file) => {
            LaunchAndStopOnEntry(client);

            int mark = client.Mark;
            _ = client.RequestOk("stepBack", new { threadId = 1 });
            Assert.Equal("entry", StoppedReason(client.WaitForEvent("stopped", mark)));
            Assert.Contains(client.Messages[mark..], m => DapClient.IsEvent(m, "output")
                && m.GetProperty("body").GetProperty("output").GetString() == "Reached the beginning of the execution history.\n");

            // 1ステップ進めて戻ると step で止まる
            mark = client.Mark;
            _ = client.RequestOk("stepIn", new { threadId = 1 });
            _ = client.WaitForEvent("stopped", mark);
            mark = client.Mark;
            _ = client.RequestOk("stepBack", new { threadId = 1 });
            Assert.Equal("step", StoppedReason(client.WaitForEvent("stopped", mark)));
            Assert.Equal(3, TopLine(client));
        });
    }

    [Fact]
    public void Disconnect_WhileRunning_StopsAndResponds() {
        WithClient(ApplicationExecutionTests.InfiniteLoop, (client, file) => {
            LaunchAndStopOnEntry(client);
            _ = client.RequestOk("continue", new { threadId = 1 });

            JsonElement response = client.Request("disconnect", new { });
            Assert.True(response.GetProperty("success").GetBoolean());
            Assert.True(client.WaitForSessionEnd());
        });
    }

    /// <summary>
    /// 実行の要求を送り，停止イベントを待つ
    /// </summary>
    internal static JsonElement RunAndWaitStopped(DapClient client, string command) {
        int mark = client.Mark;
        _ = client.RequestOk(command, new { threadId = 1 });
        return client.WaitForEvent("stopped", mark);
    }

    [Fact]
    public void ReverseContinue_StopsAtBreakpointsThenEntry() {
        WithClient(ApplicationExecutionTests.CountingLoop, (client, file) => {
            Launch(client);
            _ = client.RequestOk("setBreakpoints", Breakpoints(file, 5));
            int mark = client.Mark;
            _ = client.RequestOk("configurationDone");
            _ = client.WaitForEvent("stopped", mark);

            for(uint i = 0; i < 3; i++) {
                Assert.Equal("breakpoint", StoppedReason(RunAndWaitStopped(client, "continue")));
                Assert.Equal(i, RegisterValue(client, "$t0 ($8)"));
            }

            foreach(uint expected in new uint[] { 1, 0 }) {
                Assert.Equal("breakpoint", StoppedReason(RunAndWaitStopped(client, "reverseContinue")));
                Assert.Equal(expected, RegisterValue(client, "$t0 ($8)"));
                Assert.Equal(5, TopLine(client));
            }

            Assert.Equal("entry", StoppedReason(RunAndWaitStopped(client, "reverseContinue")));
            Assert.Equal(3, TopLine(client));
        });
    }

    private const string DataProgram = """
        .data
        value:
          .word 0x11223344
          .word 0x55667788
        .text
        main:
          la $t0, value
        """;

    private static JsonElement ReadMemory(DapClient client, object arguments) {
        return client.RequestOk("readMemory", arguments).GetProperty("body");
    }

    [Fact]
    public void ReadMemory_ReturnsDataSegment() {
        WithClient(DataProgram, (client, file) => {
            JsonElement initialize = client.RequestOk("initialize", new { adapterID = "pluspim" });
            Assert.True(initialize.GetProperty("body").GetProperty("supportsReadMemoryRequest").GetBoolean());
            int mark = client.Mark;
            _ = client.RequestOk("launch", new { });
            _ = client.WaitForEvent("initialized", mark);

            JsonElement body = ReadMemory(client, new { memoryReference = "0x10000000", count = 8 });
            Assert.Equal("0x10000000", body.GetProperty("address").GetString());
            // 44 33 22 11 88 77 66 55 (リトルエンディアン)
            Assert.Equal(Convert.ToBase64String([0x44, 0x33, 0x22, 0x11, 0x88, 0x77, 0x66, 0x55]), body.GetProperty("data").GetString());
            Assert.False(body.TryGetProperty("unreadableBytes", out _));

            // 10進数の参照と，書き込まれていない領域
            body = ReadMemory(client, new { memoryReference = "268435460", offset = 4, count = 4 });
            Assert.Equal("0x10000008", body.GetProperty("address").GetString());
            Assert.Equal("AAAAAA==", body.GetProperty("data").GetString());
        });
    }

    [Fact]
    public void ReadMemory_ClipsToAddressSpace() {
        WithClient(DataProgram, (client, file) => {
            Launch(client);

            // 0xFFFFFFFF を超える末尾は unreadableBytes
            JsonElement body = ReadMemory(client, new { memoryReference = "0xFFFFFFFC", count = 8 });
            Assert.Equal("0xFFFFFFFC", body.GetProperty("address").GetString());
            Assert.Equal(4, Convert.FromBase64String(body.GetProperty("data").GetString()!).Length);
            Assert.Equal(4, body.GetProperty("unreadableBytes").GetInt32());

            // 0 より前の部分は address を進めて表す
            body = ReadMemory(client, new { memoryReference = "0x00000002", offset = -4, count = 8 });
            Assert.Equal("0x00000000", body.GetProperty("address").GetString());
            Assert.Equal(6, Convert.FromBase64String(body.GetProperty("data").GetString()!).Length);
            Assert.False(body.TryGetProperty("unreadableBytes", out _));

            JsonElement invalid = client.Request("readMemory", new { memoryReference = "sp", count = 4 });
            Assert.False(invalid.GetProperty("success").GetBoolean());
        });
    }

    [Fact]
    public void Variables_CarryMemoryReferences() {
        WithClient(DataProgram, (client, file) => {
            LaunchAndStopOnEntry(client);

            JsonElement stackTrace = client.RequestOk("stackTrace", new { threadId = 1 });
            Assert.Equal("0x00400000", stackTrace.GetProperty("body").GetProperty("stackFrames")[0].GetProperty("instructionPointerReference").GetString());

            Dictionary<string, JsonElement> registers = Registers(client);
            Assert.Equal("0x7FFFEFFC", registers["$sp ($29)"].GetProperty("memoryReference").GetString());
            Assert.Equal("0x10008000", registers["$gp ($28)"].GetProperty("memoryReference").GetString());

            JsonElement scopes = client.RequestOk("scopes", new { frameId = 1 }).GetProperty("body").GetProperty("scopes");
            JsonElement memoryScope = scopes.EnumerateArray().Single(scope => scope.GetProperty("name").GetString() == "Memory");
            JsonElement variables = client.RequestOk("variables", new { variablesReference = memoryScope.GetProperty("variablesReference").GetInt32() })
                .GetProperty("body").GetProperty("variables");

            Assert.Equal(".data", variables[0].GetProperty("name").GetString());
            Assert.Equal("0x10000000 (8 bytes)", variables[0].GetProperty("value").GetString());
            Assert.Equal("0x10000000", variables[0].GetProperty("memoryReference").GetString());
            Assert.Equal("stack ($sp)", variables[1].GetProperty("name").GetString());
            Assert.Equal("0x7FFFEFFC", variables[1].GetProperty("memoryReference").GetString());
        });
    }

    [Fact]
    public void PseudoExpansions_ReturnsExpandedInstructions() {
        const string program = """
            .data
            msg:
              .asciiz "hi"
            .text
            main:
              la $t0, msg
              # move の前のコメント
              move $t2, $t1
              nop
              addu $t3, $t2, $zero
            """;
        WithClient(program, (client, file) => {
            _ = client.RequestOk("initialize", new { adapterID = "pluspim" });

            // 読み込み前は失敗する
            JsonElement early = client.Request("pluspimPseudoExpansions", new { source = new { path = file.FullName } });
            Assert.False(early.GetProperty("success").GetBoolean());
            Assert.Contains("Program is not loaded.", early.GetProperty("message").GetString());

            int mark = client.Mark;
            _ = client.RequestOk("launch", new { });
            _ = client.WaitForEvent("initialized", mark);

            // initialized の後 (configurationDone の前) から有効
            JsonElement body = client.RequestOk("pluspimPseudoExpansions", new { source = new { path = file.FullName } }).GetProperty("body");
            string expected = JsonSerializer.Serialize(new {
                lines = new object[] {
                    new { line = 6, mnemonic = "la", instructions = new[] {
                        new { address = "0x00400000", text = "lui $t0, 0x1000" },
                        new { address = "0x00400004", text = "ori $t0, $t0, 0x0000" } } },
                    new { line = 8, mnemonic = "move", instructions = new[] { new { address = "0x00400008", text = "addu $t2, $t1, $zero" } } },
                    new { line = 9, mnemonic = "nop", instructions = new[] { new { address = "0x0040000C", text = "sll $zero, $zero, 0" } } },
                }
            });
            Assert.Equal(expected, JsonSerializer.Serialize(body));

            // 読み込んでいないファイルは空
            JsonElement unknown = client.RequestOk("pluspimPseudoExpansions", new { source = new { path = Path.Combine(Path.GetTempPath(), "not-loaded.s") } });
            Assert.Equal(0, unknown.GetProperty("body").GetProperty("lines").GetArrayLength());
        });
    }
}
