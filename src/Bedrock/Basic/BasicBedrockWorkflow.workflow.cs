using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Bedrock.Basic;

public record BasicBedrockWorkflowArgs(string Prompt);
public record BasicBedrockWorkflowResult(string Response);

[Workflow]
public class BasicBedrockWorkflow
{
    [WorkflowRun]
    public async Task<BasicBedrockWorkflowResult> RunAsync(BasicBedrockWorkflowArgs args)
    {
        Workflow.Logger.LogInformation("Prompt: {Prompt}", args.Prompt);

        var promptResult = await Workflow.ExecuteActivityAsync(
            (IBasicBedrockActivities activities) => activities.PromptBedrockAsync(new(args.Prompt)),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(20),
            });

        Workflow.Logger.LogInformation("Response:\n{Response}", promptResult.Response);

        return new(promptResult.Response);
    }
}