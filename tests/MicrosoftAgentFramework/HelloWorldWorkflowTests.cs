namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.HelloWorld;
using Xunit;
using Xunit.Abstractions;

public class HelloWorldWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_OneModelActivity_ReturnsText()
    {
        using var fake = new FakeChatClient(ScriptedResponse.Text("Workers fall and rise / history remembers all / the run carries on"));
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("hello-world", TestSupport.PluginFor(fake)).AddWorkflow<HelloWorldWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (HelloWorldWorkflow wf) => wf.RunAsync("Write a haiku about durable execution."),
                TestSupport.WorkflowOptions(worker, "hello-world"));
            Assert.Equal("Workers fall and rise / history remembers all / the run carries on", await handle.GetResultAsync());

            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(1, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);

            var call = Assert.Single(fake.Calls);
            Assert.Equal("You are a concise assistant.", call.Options?.Instructions);
            Assert.Equal("Write a haiku about durable execution.", Assert.Single(call.Messages).Text);
        });
    }
}
