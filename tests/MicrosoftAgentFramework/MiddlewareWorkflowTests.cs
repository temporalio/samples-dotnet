namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Middleware;
using Xunit;
using Xunit.Abstractions;

public class MiddlewareWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_WrapperRefusesOneCall_ProviderEditsInstructions()
    {
        using var fake = new FakeChatClient(
            ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/etc/passwd" })),
            ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/tmp/scratch.txt" })),
            ScriptedResponse.Text("Refused /etc/passwd and deleted /tmp/scratch.txt."));
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("middleware", TestSupport.PluginFor(fake)).AddWorkflow<MiddlewareWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (MiddlewareWorkflow wf) => wf.RunAsync("Delete /etc/passwd, then delete /tmp/scratch.txt."),
                TestSupport.WorkflowOptions(worker, "middleware"));
            Assert.Equal("Refused /etc/passwd and deleted /tmp/scratch.txt.", await handle.GetResultAsync());

            // Both hooks run in the Workflow: three model Activities and nothing else.
            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(3, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);

            var refused = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.Equal("Refused: /etc/passwd is outside /tmp/", refused.Result?.ToString());
            var deleted = TestSupport.ToolResults(fake.Calls[2])[^1];
            Assert.Equal("deleted /tmp/scratch.txt", deleted.Result?.ToString());
            // The provider's sentence is appended to the agent's own instructions, not substituted for them.
            Assert.All(fake.Calls, call => Assert.Equal(
                $"You manage files. Use delete_file when asked to delete one.\n{AppendReminder.Reminder}",
                call.Options?.Instructions));
        });
    }
}
