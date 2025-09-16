using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerVersioning;

// PinnedWorkflowV1 demonstrates a workflow that likely has a short lifetime, and we want to always
// stay pinned to the same version it began on.
//
// Note that generally you won't want or need to include a version number in your workflow name if
// you're using the worker versioning feature. This sample does it to illustrate changes to the
// same code over time - but really what we're demonstrating here is the evolution of what would
// have been one workflow definition.
[Workflow("PinnedWorkflow", VersioningBehavior = VersioningBehavior.Pinned)]
public class PinnedWorkflowV1
{
    private readonly Queue<string> signals = new();

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        Workflow.Logger.LogInformation("PinnedWorkflowV1 started");

        while (true)
        {
            await Workflow.WaitConditionAsync(() => signals.Count > 0);
            var signal = signals.Dequeue();

            if (signal == "conclude")
            {
                break;
            }
        }

        await Workflow.ExecuteActivityAsync(
            (MyActivities act) => act.SomeActivity("PinnedWorkflowV1"),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

        return "PinnedWorkflowV1 result";
    }

    [WorkflowSignal]
    public Task DoNextSignalAsync(string signal)
    {
        signals.Enqueue(signal);
        return Task.CompletedTask;
    }
}
