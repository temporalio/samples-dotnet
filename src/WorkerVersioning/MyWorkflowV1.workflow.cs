using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

/// <summary>
/// The 1.0 version of the workflow we'll be making changes to.
/// </summary>
[Workflow(name: "MyWorkflow")]
public class MyWorkflowV1
{
    private bool shouldFinish;

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("Running workflow V1");
        await Workflow.WaitConditionAsync(() => shouldFinish);
    }

    [WorkflowSignal]
    public async Task ProceederAsync(string input)
    {
        await Workflow.ExecuteActivityAsync(
            () => MyActivities.Greet("V1"),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        if (input == "finish")
        {
            shouldFinish = true;
        }
    }
}