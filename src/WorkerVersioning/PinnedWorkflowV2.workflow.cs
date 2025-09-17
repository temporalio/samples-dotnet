using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

// PinnedWorkflowV2 has changes that would make it incompatible with v1, and aren't protected by
// a patch.
[Workflow("PinnedWorkflow", VersioningBehavior = VersioningBehavior.Pinned)]
public class PinnedWorkflowV2
{
    private readonly Queue<string> signals = new();

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        Workflow.Logger.LogInformation("PinnedWorkflowV2 started");

        // Here we call an activity where we didn't before, which is an incompatible change.
        await Workflow.ExecuteActivityAsync(
            (MyActivities act) => act.SomeActivity("PinnedWorkflowV2"),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

        while (true)
        {
            await Workflow.WaitConditionAsync(() => signals.Count > 0);
            var signal = signals.Dequeue();

            if (signal == "conclude")
            {
                break;
            }
        }

        // We've also changed the activity type here, another incompatible change
        await Workflow.ExecuteActivityAsync(
            (MyActivities act) => act.SomeIncompatibleActivity(new("PinnedWorkflowV2", "hi")),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

        return "PinnedWorkflowV2 result";
    }

    [WorkflowSignal]
    public async Task DoNextSignalAsync(string signal) => signals.Enqueue(signal);
}
