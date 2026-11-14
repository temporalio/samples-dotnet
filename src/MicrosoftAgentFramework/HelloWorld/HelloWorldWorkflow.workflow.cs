// @@@SNIPSTART dotnet-microsoft-agent-framework-hello-world-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.HelloWorld;

using Microsoft.Extensions.AI;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Workflows;

[Workflow]
public class HelloWorldWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string prompt)
    {
        // The Workflow carries only the model name. The worker resolves it to the OpenAI client
        // and runs each model call as the MicrosoftAgentFramework.InvokeModel Activity.
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            Name = "assistant",
            ChatOptions = new ChatOptions { Instructions = "You are a concise assistant." },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        var response = await agent.RunAsync(prompt);
        return response.Text;
    }
}
// @@@SNIPEND
