namespace TemporalioSamples.OpenTelemetry;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        Workflow.Logger.LogInformation("Running workflow {WorkflowId}.", Temporalio.Workflows.Workflow.Info.WorkflowId);

        await Workflow.ExecuteActivityAsync(
            () => Activities.MyActivity("input"),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });

        return "complete!";
    }
}