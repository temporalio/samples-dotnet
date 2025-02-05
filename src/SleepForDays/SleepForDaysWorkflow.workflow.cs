namespace TemporalioSamples.SleepForDays;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class SleepForDaysWorkflow
{
    private bool complete;

    [WorkflowRun]
    public async Task RunAsync()
    {
        while (!complete)
        {
            await Workflow.ExecuteActivityAsync(
                (Activities act) => act.SendEmail("Sleeping for 30 days"),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            await Workflow.WhenAnyAsync(
                Workflow.DelayAsync(TimeSpan.FromDays(30)),
                Workflow.WaitConditionAsync(() => complete));
        }

        Workflow.Logger.LogInformation("done!");
    }

    [WorkflowSignal]
    public async Task CompleteAsync() => complete = true;
}