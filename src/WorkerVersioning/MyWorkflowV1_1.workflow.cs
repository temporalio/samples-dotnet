using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

/// <summary>
/// The 1.1 version of the workflow, which is compatible with the first version.
///
/// The compatible changes we've made are:
///     - Altering the log lines
///     - Using the `patched` API to properly introduce branching behavior while maintaining
///       compatibility.
/// </summary>
[Workflow(name: "MyWorkflow")]
public class MyWorkflowV1Dot1
{
    private bool shouldFinish;

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("Running workflow V1.1");
        await Workflow.WaitConditionAsync(() => shouldFinish);
    }

    [WorkflowSignal]
    public async Task ProceederAsync(string input)
    {
        if (Workflow.Patched("different-activity"))
        {
            await Workflow.ExecuteActivityAsync(
                () => MyActivities.SuperGreet("V1.1", 100),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
        else
        {
            await Workflow.ExecuteActivityAsync(
                () => MyActivities.Greet("V1.1"),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }

        if (input == "finish")
        {
            shouldFinish = true;
        }
    }
}