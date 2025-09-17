using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

// AutoUpgradingWorkflowV1Dot1 represents us having made *compatible* changes to
// AutoUpgradingWorkflowV1.
//
// The compatible changes we've made are:
//   - Altering the log lines
//   - Using the workflow.patched API to properly introduce branching behavior while maintaining
//     compatibility
[Workflow("AutoUpgradingWorkflow", VersioningBehavior = VersioningBehavior.AutoUpgrade)]
public class AutoUpgradingWorkflowV1Dot1
{
    private readonly Queue<string> signals = new();

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        Workflow.Logger.LogInformation("AutoUpgradingWorkflowV1b started");

        while (true)
        {
            await Workflow.WaitConditionAsync(() => signals.Count > 0);
            var signal = signals.Dequeue();

            if (signal == "do-activity")
            {
                if (Workflow.Patched("DifferentActivity"))
                {
                    await Workflow.ExecuteActivityAsync(
                        (MyActivities act) => act.SomeIncompatibleActivity(new("AutoUpgradingWorkflowV1Dot1", "hi")),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                }
                else
                {
                    // Note it is a valid compatible change to alter the input to an activity.
                    // However, because we're using the patched API, this branch will never be
                    // taken.
                    await Workflow.ExecuteActivityAsync(
                        (MyActivities act) => act.SomeActivity("AutoUpgradingWorkflowV1b"),
                        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                }
            }
            else
            {
                Workflow.Logger.LogInformation("AutoUpgradingWorkflowV1b concluding");
                return "AutoUpgradingWorkflowV1b result";
            }
        }
    }

    [WorkflowSignal]
    public async Task DoNextSignalAsync(string signal) => signals.Enqueue(signal);
}
