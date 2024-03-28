namespace TemporalioSamples.Timer;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class Subscription
{
    [WorkflowRun]
    public async Task RunAsync(string userId)
    {
        try
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
        catch (OperationCanceledException)
        {
            Workflow.Logger.LogInformation("Workflow cancelled, cleaning up...");
            // Handle any cleanup here
            // Re-throw to close the workflow as Cancelled. Otherwise, it will be closed as Completed.
            throw;
        }
    }
}