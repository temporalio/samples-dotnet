using Temporalio.Workflows;

namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Implements batch processing by running multiple <see cref="SlidingWindowBatchWorkflow"/>
/// instances in parallel.
/// </summary>
[Workflow]
public class BatchWorkflow
{
    /// <summary>
    /// Processes a batch of records using multiple parallel sliding window workflows.
    /// </summary>
    /// <param name="pageSize">Number of records to start processing in a single sliding window
    /// workflow run.</param>
    /// <param name="slidingWindowSize">Number of records to process in parallel by a single
    /// sliding window workflow. Can be larger than <paramref name="pageSize"/>.</param>
    /// <param name="partitions">Number of SlidingWindowBatchWorkflows to run in parallel. If the
    /// number of partitions is too low, the update rate of a single SlidingWindowBatchWorkflow
    /// can get too high.</param>
    /// <returns>Total number of processed records.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync(int pageSize, int slidingWindowSize, int partitions)
    {
        // The sample partitions the dataset into contiguous ranges. A real application can
        // choose any other way to divide the records into multiple collections.
        var totalCount = await Workflow.ExecuteActivityAsync(
            () => RecordLoaderActivities.GetRecordCount(),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) });

        var partitionSize = (totalCount / partitions) + (totalCount % partitions > 0 ? 1 : 0);

        var partitionResults = new List<Task<int>>(partitions);
        for (var i = 0; i < partitions; i++)
        {
            // Makes the child id more user-friendly.
            var childId = $"{Workflow.Info.WorkflowId}/{i}";

            // Defines the partition boundaries.
            var offset = partitionSize * i;
            var maximumOffset = Math.Min(offset + partitionSize, totalCount);

            var input = new ProcessBatchInput
            {
                PageSize = pageSize,
                SlidingWindowSize = slidingWindowSize,
                Offset = offset,
                MaximumOffset = maximumOffset,
            };

            partitionResults.Add(Workflow.ExecuteChildWorkflowAsync(
                (SlidingWindowBatchWorkflow wf) => wf.RunAsync(input),
                new() { Id = childId }));
        }

        var results = await Workflow.WhenAllAsync(partitionResults);
        return results.Sum();
    }
}
