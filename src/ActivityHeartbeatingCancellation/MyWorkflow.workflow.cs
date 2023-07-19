using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace TemporalioSamples.ActivityHeartbeatingCancellation;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        try
        {
            await Workflow.ExecuteActivityAsync(
                () => MyActivities.FakeProgressAsync(1000),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromMinutes(5),
                    HeartbeatTimeout = TimeSpan.FromSeconds(3),
                    // Don't send rejection to our Workflow until the Activity has confirmed cancellation
                    CancellationType = ActivityCancellationType.WaitCancellationCompleted,
                });
        }
        catch (TemporalException e) when (TemporalException.IsCanceledException(e))
        {
            // This catches the cancel just for demonstration, you usually don't want to catch it
            Workflow.Logger.LogInformation("Workflow cancelled along with its activity");
            throw;
        }
    }
}