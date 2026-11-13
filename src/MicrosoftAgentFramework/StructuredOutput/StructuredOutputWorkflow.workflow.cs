// @@@SNIPSTART dotnet-microsoft-agent-framework-structured-output-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.StructuredOutput;

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Workflows;

public record PersonInfo(
    [property: Description("Name of the person")] string Name,
    [property: Description("Age of the person")] int Age,
    [property: Description("Occupation of the person")] string Occupation);

[Workflow]
public class StructuredOutputWorkflow
{
    [WorkflowRun]
    public async Task<PersonInfo> RunAsync(string text)
    {
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "Extract the person's details from the text." },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        // RunAsync<T> sends the JSON schema of PersonInfo to the model Activity as the response
        // format and parses the reply. The Workflow returns the record itself.
        var response = await agent.RunAsync<PersonInfo>(text);
        return response.Result;
    }
}
// @@@SNIPEND
