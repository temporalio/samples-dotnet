namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Tools;
using Xunit;
using Xunit.Abstractions;

public class ToolsWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_WorkflowToolAndActivityTool_OneActivityForTheActivityTool()
    {
        using var fake = new FakeChatClient(
            ScriptedResponse.ToolCalls(
                ("CountLetters", new Dictionary<string, object?> { ["word"] = "strawberry", ["letter"] = "r" }),
                ("GetWeather", new Dictionary<string, object?> { ["city"] = "San Francisco" })),
            ScriptedResponse.Text("Three R's. San Francisco is 72F and sunny."));
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("tools", TestSupport.PluginFor(fake)).
                AddActivity(WeatherActivities.GetWeatherAsync).
                AddWorkflow<ToolsWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (ToolsWorkflow wf) => wf.RunAsync("Count the R's in strawberry and look up the weather in San Francisco."),
                TestSupport.WorkflowOptions(worker, "tools"));
            Assert.Equal("Three R's. San Francisco is 72F and sunny.", await handle.GetResultAsync());

            // The in-Workflow tool leaves no Activity; the ActivityAsTool call is one GetWeather Activity.
            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(2, counts[TestSupport.InvokeModel]);
            Assert.Equal(1, counts["GetWeather"]);
            Assert.Equal(2, counts.Count);

            Assert.Equal(2, fake.CallCount);
            var declared = fake.Calls[0].Options?.Tools?.Select(tool => tool.Name).ToList();
            Assert.Equal("CountLetters", declared?[0]);
            Assert.Equal("GetWeather", declared?[1]);
            var results = TestSupport.ToolResults(fake.Calls[1]);
            Assert.Equal(2, results.Count);
            Assert.Equal("3", results[0].Result?.ToString());
            Assert.Contains("sunny", results[1].Result?.ToString(), StringComparison.Ordinal);
        });
    }
}
