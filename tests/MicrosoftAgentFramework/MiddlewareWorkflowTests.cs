namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Middleware;
using Xunit;
using Xunit.Abstractions;

public class MiddlewareWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env) : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task RunAsync_WrapperRefusesOneCall_ProviderEditsInstructions()
    {
        using var fake = new FakeChatClient(
            ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/etc/passwd" })),
            ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/tmp/../etc/passwd" })),
            ScriptedResponse.ToolCalls(("delete_file", new Dictionary<string, object?> { ["path"] = "/tmp/scratch.txt" })),
            ScriptedResponse.Text("Refused /etc/passwd and deleted /tmp/scratch.txt."));
        using var worker = new TemporalWorker(
            Client,
            TestSupport.WorkerOptions("middleware", TestSupport.PluginFor(fake)).AddWorkflow<MiddlewareWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (MiddlewareWorkflow wf) => wf.RunAsync("Delete /etc/passwd, then delete /tmp/scratch.txt."),
                TestSupport.WorkflowOptions(worker, "middleware"));
            Assert.Equal("Refused /etc/passwd and deleted /tmp/scratch.txt.", await handle.GetResultAsync());

            // Both hooks run in the Workflow: four model Activities and nothing else.
            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(4, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);

            var refused = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.Equal("Refused: /etc/passwd is outside /tmp/", refused.Result?.ToString());
            // A path that climbs out of /tmp/ is normalized before the check.
            var climbed = TestSupport.ToolResults(fake.Calls[2])[^1];
            Assert.Equal("Refused: /tmp/../etc/passwd is outside /tmp/", climbed.Result?.ToString());
            var deleted = TestSupport.ToolResults(fake.Calls[3])[^1];
            Assert.Equal("deleted /tmp/scratch.txt", deleted.Result?.ToString());
            // The provider's sentence is appended to the agent's own instructions, not substituted for them.
            Assert.All(fake.Calls, call => Assert.Equal(
                $"You manage files. Use delete_file when asked to delete one.\n{AppendReminder.Reminder}",
                call.Options?.Instructions));
        });
    }
}
