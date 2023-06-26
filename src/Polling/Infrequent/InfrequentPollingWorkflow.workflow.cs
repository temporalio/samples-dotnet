namespace TemporalioSamples.Polling.Infrequent;

using Temporalio.Workflows;

[Workflow]
public class InfrequentPollingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Infrequent polling via activity can be implemented via activity retries.
        // For this sample we want to poll the test service every 60 seconds.
        //
        // Here we:
        // - Set RetryPolicy backoff coefficient of 1
        // - Set initial interval to the poll frequency (since coefficient is 1, same interval will be used for all retries)
        //
        // With this in case our test service is "down" we can fail our activity and it will be retried based on our 60 second retry
        // interval until poll is successful and we can return a result from the activity.
        var result = await Workflow.ExecuteActivityAsync(
            (InfrequentPollingActivity act) => act.DoPollAsync(),
            new()
            {
                // Set activity StartToClose timeout (single activity exec), does not include retries
                StartToCloseTimeout = TimeSpan.FromSeconds(2),
                RetryPolicy = new()
                {
                    BackoffCoefficient = 1,
                    InitialInterval = TimeSpan.FromSeconds(60),
                },
            });

        return result;
    }
}