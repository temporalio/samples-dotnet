namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.Exceptions;
using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.HumanInTheLoop;
using Xunit;
using Xunit.Abstractions;

public class HumanInTheLoopWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_ApprovedThroughUpdate_ToolRunsOnce()
    {
        using var fake = DeleteFake();
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("human-in-the-loop", TestSupport.PluginFor(fake)).AddWorkflow<HumanInTheLoopWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (HumanInTheLoopWorkflow wf) => wf.RunAsync("Please delete /tmp/report.txt"),
                TestSupport.WorkflowOptions(worker, "human-in-the-loop"));

            var request = await AssertMore.EventuallyAsync(
                async () => Assert.Single(await handle.QueryAsync(wf => wf.PendingApprovals())),
                iterations: 40);
            Assert.Equal("delete_file", request.ToolName);
            Assert.Equal("/tmp/report.txt", request.Arguments["path"]?.ToString());
            Assert.Equal(1, fake.CallCount);

            // An empty id is rejected by the validator and an unknown id by the handler; both fail
            // the Update, not the Workflow.
            await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision(string.Empty, true))));
            await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision("nope", true))));
            Assert.Equal("approved", await handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision(request.RequestId, true))));
            Assert.Equal("Deleted /tmp/report.txt.", await handle.GetResultAsync());

            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(2, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);
            var result = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.Equal("deleted /tmp/report.txt", result.Result?.ToString());
        });
    }

    [Fact]
    public async Task RunAsync_RejectedThroughUpdate_ToolNeverRuns()
    {
        using var fake = DeleteFake();
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("human-in-the-loop", TestSupport.PluginFor(fake)).AddWorkflow<HumanInTheLoopWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (HumanInTheLoopWorkflow wf) => wf.RunAsync("Please delete /tmp/report.txt"),
                TestSupport.WorkflowOptions(worker, "human-in-the-loop"));
            var request = await AssertMore.EventuallyAsync(
                async () => Assert.Single(await handle.QueryAsync(wf => wf.PendingApprovals())),
                iterations: 40);

            Assert.Equal("rejected", await handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision(request.RequestId, false))));
            Assert.Equal("Deleted /tmp/report.txt.", await handle.GetResultAsync());

            var result = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.StartsWith("Tool call invocation was rejected by the approver", result.Result?.ToString(), StringComparison.Ordinal);
        });
    }

    private static FakeChatClient DeleteFake() => new(
        ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/tmp/report.txt" })),
        ScriptedResponse.Text("Deleted /tmp/report.txt."));
}
