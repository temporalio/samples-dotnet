namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.StructuredOutput;
using Xunit;
using Xunit.Abstractions;

public class StructuredOutputWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_SchemaSentToModel_RecordReturned()
    {
        using var fake = new FakeChatClient(ScriptedResponse.Json(new PersonInfo("John Smith", 30, "software engineer")));
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("structured-output", TestSupport.PluginFor(fake)).AddWorkflow<StructuredOutputWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (StructuredOutputWorkflow wf) => wf.RunAsync("John Smith is a 30 year-old software engineer."),
                TestSupport.WorkflowOptions(worker, "structured-output"));
            Assert.Equal(new PersonInfo("John Smith", 30, "software engineer"), await handle.GetResultAsync());

            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(1, counts[TestSupport.InvokeModel]);
            Assert.Single(counts);
            var format = Assert.IsType<ChatResponseFormatJson>(Assert.Single(fake.Calls).Options?.ResponseFormat);
            Assert.Contains("occupation", format.Schema?.GetRawText(), StringComparison.Ordinal);
        });
    }
}
