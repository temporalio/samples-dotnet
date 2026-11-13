namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Mcp;
using Xunit;
using Xunit.Abstractions;

public class McpWorkflowTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task RunAsync_ListsAndCallsTheEchoTool_AsActivities()
    {
        // The sample's tools are served in memory instead of over the stdio subprocess the worker spawns.
        await using var mcp = new InMemoryMcpServer(EchoMcpServer.CreateTools());
        using var fake = new FakeChatClient(
            ScriptedResponse.ToolCalls(("echo", new Dictionary<string, object?> { ["message"] = "hello from MCP" })),
            ScriptedResponse.Text("The echo tool returned: hello from MCP"));
        await using var env = await WorkflowEnvironment.StartLocalAsync(new() { LoggerFactory = LoggerFactory, });
        using var worker = new TemporalWorker(
            env.Client,
            TestSupport.WorkerOptions("mcp", TestSupport.PluginFor(fake, "echo", mcp.ClientTransport)).AddWorkflow<McpWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (McpWorkflow wf) => wf.RunAsync("Use the echo tool to echo the message 'hello from MCP'."),
                TestSupport.WorkflowOptions(worker, "mcp"));
            Assert.Equal("The echo tool returned: hello from MCP", await handle.GetResultAsync());

            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(1, counts[TestSupport.McpListTools]);
            Assert.Equal(2, counts[TestSupport.InvokeModel]);
            Assert.Equal(1, counts[TestSupport.McpCallTool]);
            Assert.Equal(3, counts.Count);

            Assert.Equal(1, mcp.ListToolsRequestCount);
            Assert.Equal(1, mcp.CallToolRequestCount);
            var declared = Assert.Single(fake.Calls[0].Options!.Tools!);
            Assert.Equal("echo", declared.Name);
            var result = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.Contains("hello from MCP", result.Result?.ToString(), StringComparison.Ordinal);
        });
    }
}
