namespace TemporalioSamples.Polling.PeriodicSequence;

using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class PeriodicPollingChildWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            // Here we would invoke a sequence of activities
            // For sample we just use a single one
            try
            {
                return await Workflow.ExecuteActivityAsync(
                    (PeriodicPollingActivity a) => a.DoPollAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(5),
                        RetryPolicy = new()
                        {
                            MaximumAttempts = 1,
                        },
                    });
            }
            catch (ActivityFailureException)
            {
            }

            await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
        }

        // Request that the new child workflow run is invoked
        throw Workflow.CreateContinueAsNewException((PeriodicPollingChildWorkflow wf) => wf.RunAsync());
    }
}