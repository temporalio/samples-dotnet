using Temporalio.Workflows;

namespace RetryAndHeartbeat;

[Workflow]
public class RetryAndHeartbeatWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string input)
    {
        var options = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30),
            HeartbeatTimeout = TimeSpan.FromSeconds(5),
            RetryPolicy = new()
            {
                MaximumAttempts = 3,
                InitialInterval = TimeSpan.FromSeconds(1),
                BackoffCoefficient = 2.0,
                MaximumInterval = TimeSpan.FromSeconds(10)
            }
        };

        return await Workflow.ExecuteActivityAsync(
            (RetryAndHeartbeatActivity a) => a.ProcessAsync(input),
            options);
    }
}
