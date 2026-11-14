// @@@SNIPSTART dotnet-microsoft-agent-framework-mcp-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.Mcp;

using Microsoft.Extensions.AI;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Extensions.MicrosoftAgentFramework.Mcp;
using Temporalio.Workflows;

[Workflow]
public class McpWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string prompt)
    {
        // One MicrosoftAgentFramework.McpListTools Activity returns a tool per server entry, with
        // the server's input schema. Each call the model makes is one
        // MicrosoftAgentFramework.McpCallTool Activity. The connection lives on the worker.
        var tools = await TemporalMcpServer.ListToolsAsync("echo", new McpToolOptions
        {
            ListToolsActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) },
            CallToolActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) },
        });
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "Use the echo tool when asked to echo something.",
                Tools = tools.Cast<AITool>().ToList(),
            },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        var response = await agent.RunAsync(prompt);
        return response.Text;
    }
}
// @@@SNIPEND
