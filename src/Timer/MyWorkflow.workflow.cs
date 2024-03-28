namespace TemporalioSamples.Timer;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string userId)
    {
        while (true)
        {
            await Workflow.DelayAsync(TimeSpan.FromDays(30));

            var result = await Workflow.ExecuteActivityAsync(
                () => MyActivities.Charge(userId),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
            Workflow.Logger.LogInformation("Activity result: {Result}", result);
        }
    }
}