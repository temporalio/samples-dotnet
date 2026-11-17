namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using System.Reflection;
using ModelContextProtocol.Client;
using Temporalio.Extensions.MicrosoftAgentFramework.Testing;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Mcp;
using Xunit;
using Xunit.Abstractions;

public class McpWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env) : WorkflowEnvironmentTestBase(output, env)
{
    [Fact]
    public async Task RunAsync_ListsAndCallsTheEchoTool_AsActivities()
    {
        // The sample's tools served in memory; the next test covers the stdio shape the worker uses.
        await using var mcp = new InMemoryMcpServer(EchoMcpServer.CreateTools());
        await RunEchoAsync(mcp.ClientTransport, "mcp");
        Assert.Equal(1, mcp.ListToolsRequestCount);
        Assert.Equal(1, mcp.CallToolRequestCount);
    }

    [Fact]
    public async Task RunAsync_ServesTheEchoToolOverStdio()
    {
        // The worker's own launch shape: this sample as a child process with the mcp-server command.
        // The DLL is run from the sample's own output directory (the test project passes the path as
        // assembly metadata), because that directory carries the sample's whole dependency closure.
        // The command runs without the OPENAI_API_KEY check, so no key is needed.
        var outputDirectory = typeof(McpWorkflowTests).Assembly.
            GetCustomAttributes<AssemblyMetadataAttribute>().
            Single(attribute => attribute.Key == "MicrosoftAgentFrameworkMcpOutputDirectory").Value!;
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "echo",
            Command = "dotnet",
            Arguments = new List<string> { Path.Combine(outputDirectory, "TemporalioSamples.MicrosoftAgentFramework.Mcp.dll"), "mcp-server" },
        });
        await RunEchoAsync(transport, "mcp-stdio");
    }

    private async Task RunEchoAsync(IClientTransport transport, string scenario)
    {
        using var fake = new FakeChatClient(
            ScriptedResponse.ToolCalls(("echo", new Dictionary<string, object?> { ["message"] = "hello from MCP" })),
            ScriptedResponse.Text("The echo tool returned: hello from MCP"));
        using var worker = new TemporalWorker(
            Client,
            TestSupport.WorkerOptions(scenario, TestSupport.PluginFor(fake, "echo", transport)).AddWorkflow<McpWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (McpWorkflow wf) => wf.RunAsync("Use the echo tool to echo the message 'hello from MCP'."),
                TestSupport.WorkflowOptions(worker, scenario));
            Assert.Equal("The echo tool returned: hello from MCP", await handle.GetResultAsync());

            var counts = await TestSupport.ActivityCountsAsync(handle);
            Assert.Equal(1, counts[TestSupport.McpListTools]);
            Assert.Equal(2, counts[TestSupport.InvokeModel]);
            Assert.Equal(1, counts[TestSupport.McpCallTool]);
            Assert.Equal(3, counts.Count);

            var declared = Assert.Single(fake.Calls[0].Options!.Tools!);
            Assert.Equal("echo", declared.Name);
            var result = Assert.Single(TestSupport.ToolResults(fake.Calls[1]));
            Assert.Contains("hello from MCP", result.Result?.ToString(), StringComparison.Ordinal);
        });
    }
}
