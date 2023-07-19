using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

/// <summary>
/// The 2.0 version of the workflow, which is fully incompatible with the other workflows, since it
/// alters the sequence of commands without using `patched`.
/// </summary>
[Workflow(name: "MyWorkflow")]
public class MyWorkflowV2
{
    private bool shouldFinish;

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("Running workflow V2");
        await Workflow.WaitConditionAsync(() => shouldFinish);
    }

    [WorkflowSignal]
    public async Task ProceederAsync(string input)
    {
        await Workflow.DelayAsync(1000);
        await Workflow.ExecuteActivityAsync(
            () => MyActivities.SuperGreet("V2", 100),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        await Workflow.ExecuteActivityAsync(
            () => MyActivities.Greet("V2"),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        if (input == "finish")
        {
            shouldFinish = true;
        }
    }
}