using Temporalio.Exceptions;
using Temporalio.Workflows;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.PeriodicSequence;

[Workflow]
public class PerilodPollingChildWorkflow : IPollingChildWorkflow
{
    private int singleWorkflowPollAttempts = 10;

    [WorkflowRun]
    public async Task<string> RunAsync(PollingChildWorkflowArgs args)
    {
        for (var i = 0; i < singleWorkflowPollAttempts; i++)
        {
            // Here we would invoke a sequence of activities
            // For sample we just use a single one
            try
            {
                return await Workflow.ExecuteActivityAsync(
                    (PeriodicPollingActivity a) => a.DoPollAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromSeconds(4),
                        RetryPolicy = new()
                        {
                            MaximumAttempts = 1,
                        },
                    });
            }
            catch (ActivityFailureException)
            {
                // Log error after retries exhausted
            }

            await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
        }

        // Request that the new child workflow run is invoked
        throw Workflow.CreateContinueAsNewException((PerilodPollingChildWorkflow wf) => wf.RunAsync(args));
    }
}