using Temporalio.Workflows;

namespace TemporalioSamples.Batch.HeartbeatingActivity;

/// <summary>
/// A sample implementation of processing a batch by a single activity.
///
/// <para>An activity can run for as long as needed. It reports that it is still alive through a
/// heartbeat. If the worker is restarted, the activity is retried after the heartbeat timeout.
/// </para>
/// </summary>
[Workflow]
public class HeartbeatingActivityBatchWorkflow
{
    /// <summary>
    /// Processes the batch of records.
    /// </summary>
    /// <returns>Total number of processed records.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync()
    {
        // No special logic needed here, as the activity is retried automatically by the service.
        //
        // The start-to-close timeout is set to a high value to support large batch sizes.
        // A heartbeat timeout is required to quickly restart the activity in case of failures,
        // and to record heartbeat details at the service.
        return await Workflow.ExecuteActivityAsync(
            () => RecordProcessorActivities.ProcessRecordsAsync(),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromHours(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(10),
            });
    }
}
