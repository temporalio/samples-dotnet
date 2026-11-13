// @@@SNIPSTART dotnet-microsoft-agent-framework-tools-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.Tools;

using Microsoft.Extensions.AI;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Workflows;

[Workflow]
public class ToolsWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string prompt)
    {
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "Use the tools you are given and answer briefly.",
                Tools = new List<AITool>
                {
                    // CountLetters runs in the Workflow.
                    AIFunctionFactory.Create(LetterTools.CountLetters),
                    // Each GetWeather call runs the Activity with its own timeout and retries.
                    ActivityAsTool.Create(
                        () => WeatherActivities.GetWeatherAsync(default!),
                        new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) }),
                },
            },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        var response = await agent.RunAsync(prompt);
        return response.Text;
    }
}
// @@@SNIPEND
