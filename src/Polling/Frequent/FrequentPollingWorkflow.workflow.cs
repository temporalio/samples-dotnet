using Temporalio.Workflows;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.Frequent;

[Workflow]
public class FrequentPollingWorkflow : IPollingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Frequent polling (1 second or faster) should be done inside the activity itself. Note that the activity has to heart beat
        // on each iteration. Note that we need to set our HeartbeatTimeout in ActivityOptions shorter than the StartToClose timeout.
        // You can use an appropriate activity retry policy for your activity.
        var result = await Workflow.ExecuteActivityAsync(
            (FrequentPollingActivity act) => act.DoPollAsync(),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(60),
                HeartbeatTimeout = TimeSpan.FromSeconds(2),
            });

        return result;
    }
}