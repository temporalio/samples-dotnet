using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Bedrock.Basic;

[Workflow]
public class BedrockWorkflow
{
    public record WorkflowArgs(string Prompt);

    public record WorkflowResult(string Response);

    [WorkflowRun]
    public async Task<WorkflowResult> RunAsync(WorkflowArgs args)
    {
        Workflow.Logger.LogInformation("Prompt: {Prompt}", args.Prompt);

        var promptResult = await Workflow.ExecuteActivityAsync(
            (BedrockActivities activities) => activities.PromptBedrockAsync(new(args.Prompt)),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(20),
            });

        Workflow.Logger.LogInformation("Response:\n{Response}", promptResult.Response);

        return new(promptResult.Response);
    }
}