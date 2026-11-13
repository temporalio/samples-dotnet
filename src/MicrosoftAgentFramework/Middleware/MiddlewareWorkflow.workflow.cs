// @@@SNIPSTART dotnet-microsoft-agent-framework-middleware-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.Middleware;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Workflows;

[Workflow]
public class MiddlewareWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string prompt)
    {
        var deleteFile = AIFunctionFactory.Create(
            (string path) => $"deleted {path}",
            "delete_file",
            "Deletes the file at the given path.");
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You manage files. Use delete_file when asked to delete one.",
                Tools = new List<AITool> { new BlockOutsideTmp(deleteFile) },
            },
            AIContextProviders = new AIContextProvider[] { new AppendReminder() },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        var response = await agent.RunAsync(prompt);
        return response.Text;
    }
}
// @@@SNIPEND
