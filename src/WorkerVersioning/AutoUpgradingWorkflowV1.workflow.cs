using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

// AutoUpgradingWorkflowV1 will automatically move to the latest worker version. We'll be making
// changes to it, which must be replay safe.
//
// Note that generally you won't want or need to include a version number in your
// workflow name if you're using the worker versioning feature. This sample does it
// to illustrate changes to the same code over time - but really what we're
// demonstrating here is the evolution of what would have been one workflow definition.
[Workflow("AutoUpgradingWorkflow", VersioningBehavior = VersioningBehavior.AutoUpgrade)]
public class AutoUpgradingWorkflowV1
{
    private readonly Queue<string> signals = new();

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        Workflow.Logger.LogInformation("AutoUpgradingWorkflowV1 started");

        while (true)
        {
            await Workflow.WaitConditionAsync(() => signals.Count > 0);
            var signal = signals.Dequeue();

            if (signal == "do-activity")
            {
                await Workflow.ExecuteActivityAsync(
                    (MyActivities act) => act.SomeActivity("AutoUpgradingWorkflowV1"),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            }
            else
            {
                Workflow.Logger.LogInformation("AutoUpgradingWorkflowV1 concluding");
                return "AutoUpgradingWorkflowV1 result";
            }
        }
    }

    [WorkflowSignal]
    public Task DoNextSignalAsync(string signal)
    {
        signals.Enqueue(signal);
        return Task.CompletedTask;
    }
}
