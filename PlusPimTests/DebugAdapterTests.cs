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
            Assert.True(client.Adapter.WaitForSessionEnd().IsCompleted);
        });
    }
}
