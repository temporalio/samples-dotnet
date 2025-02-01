namespace TemporalioSamples.SleepForDays;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class SleepForDaysWorkflow
{
    private bool isComplete;

    [WorkflowRun]
    public async Task RunAsync()
    {
        isComplete = false;
        while (!isComplete)
        {
            await Workflow.ExecuteActivityAsync(
                (SleepForDaysActivities act) => act.SendEmail("Sleeping for 30 days"),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            await Task.WhenAny(
                Workflow.DelayAsync(TimeSpan.FromSeconds(30)),
                Workflow.WaitConditionAsync(() => isComplete));
        }

        Workflow.Logger.LogInformation("done!");
    }

    [WorkflowSignal]
    public async Task CompleteAsync()
    {
        isComplete = true;
    }
}