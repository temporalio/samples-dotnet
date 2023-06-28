namespace TemporalioSamples.Polling.Frequent;

using Temporalio.Workflows;

[Workflow]
public class FrequentPollingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Frequent polling (1 second or faster) should be done inside the activity itself. Note that the activity has to heart beat
        // on each iteration. Note that we need to set our HeartbeatTimeout in ActivityOptions shorter than the StartToClose timeout.
        // You can use an appropriate activity retry policy for your activity.
        var result = await Workflow.ExecuteActivityAsync(
            (FrequentPollingActivities act) => act.DoPollAsync(),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(60),
                HeartbeatTimeout = TimeSpan.FromSeconds(2),
            });

        return result;
    }
}