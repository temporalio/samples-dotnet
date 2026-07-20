using Temporalio.Workflows;

namespace TemporalioSamples.Batch.Iterator;

/// <summary>
/// Implements the iterator workflow pattern.
///
/// <para>A single workflow run processes a single page of records in parallel. Each record is
/// processed using its own <see cref="RecordProcessorWorkflow"/> child workflow.</para>
///
/// <para>After all child workflows complete, a new run of the parent workflow is created using
/// continue-as-new. The new run processes the next page of records. This way a practically
/// unlimited set of records can be processed.</para>
/// </summary>
[Workflow]
public class IteratorBatchWorkflow
{
    /// <summary>
    /// Processes the batch of records.
    /// </summary>
    /// <param name="pageSize">Number of records to process in a single workflow run.</param>
    /// <param name="offset">Offset of the first record to process. 0 to start the batch
    /// processing.</param>
    /// <returns>Total number of processed records.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync(int pageSize, int offset)
    {
        // Loads a page of records.
        var records = await Workflow.ExecuteActivityAsync(
            () => RecordLoaderActivities.GetRecords(pageSize, offset),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) });

        // Starts a child workflow per record asynchronously.
        var results = records.Select(record =>
        {
            // Uses a human-friendly child id.
            var childId = $"{Workflow.Info.WorkflowId}/{record.Id}";
            return Workflow.ExecuteChildWorkflowAsync(
                (RecordProcessorWorkflow wf) => wf.RunAsync(record),
                new() { Id = childId });
        }).ToList();

        // Waits for all children to complete.
        await Workflow.WhenAllAsync(results);

        // Skips error handling for the sample's brevity, so failed RecordProcessorWorkflows are
        // ignored.

        // No more records in the dataset. Completes the workflow.
        if (records.Count == 0)
        {
            return offset;
        }

        // Continues-as-new with the increased offset.
        throw Workflow.CreateContinueAsNewException(
            (IteratorBatchWorkflow wf) => wf.RunAsync(pageSize, offset + records.Count));
    }
}
