using Temporalio.Workflows;

namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Implements batch processing by running a specified number of record processing child
/// workflows in parallel. A new record processing child workflow is started when a previously
/// started one completes. Child completion is reported through the
/// <see cref="ReportCompletionAsync"/> signal, since it is not possible to passively wait for a
/// child that was started by a previous continue-as-new run.
///
/// <para>Calls continue-as-new after starting <c>PageSize</c> children. Note that the sliding
/// window size can be larger than <c>PageSize</c>.</para>
/// </summary>
[Workflow]
public class SlidingWindowBatchWorkflow
{
    /// <summary>
    /// Accumulates records to remove for signals delivered before <see cref="RunAsync"/> starts
    /// executing. This is the workaround for the race between continue-as-new and a child
    /// reporting completion: a signal can be delivered to the new run before its workflow run
    /// method has had a chance to populate <see cref="currentRecords"/> from the input.
    /// </summary>
    private readonly HashSet<int> recordsToRemove = new();

    /// <summary>
    /// Ids of records that are being processed by child workflows. Null until the workflow run
    /// method has initialized it from the input, which lets <see cref="ReportCompletionAsync"/>
    /// detect signals that arrive before that point.
    /// </summary>
    private HashSet<int>? currentRecords;

    /// <summary>
    /// Count of completed record processing child workflows.
    /// </summary>
    private int progress;

    /// <summary>
    /// Processes the batch of records.
    /// </summary>
    /// <param name="input">Defines the range of records to process and, on continue-as-new, the
    /// progress carried over from the previous run.</param>
    /// <returns>Total number of processed records.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync(ProcessBatchInput input)
    {
        progress = input.Progress;

        // Alias as a non-nullable local so it can be captured by the WaitConditionAsync
        // lambdas below. It is the same set instance as the currentRecords field, so mutations
        // through either reference are visible to both.
        var trackedRecords = input.CurrentRecords;
        currentRecords = trackedRecords;

        // Remove records for signals delivered before this run started.
        var countBefore = trackedRecords.Count;
        trackedRecords.ExceptWith(recordsToRemove);
        progress += countBefore - trackedRecords.Count;

        var pageSize = input.PageSize;
        var offset = input.Offset;
        var slidingWindowSize = input.SlidingWindowSize;

        // For ease of testing, forces continue-as-new well before the real history-size
        // threshold would suggest it, instead of relying on Workflow.ContinueAsNewSuggested
        // alone. Mirrors ClusterManagerWorkflow's maxHistoryLength in the SafeMessageHandlers
        // sample. Each child adds several history events of its own (start command, started
        // event, completion Signal), so this needs to be well above that per-child footprint or
        // a run ends up continuing-as-new after only one or two children.
        var maxHistoryLength = input.TestContinueAsNew ? 100 : int.MaxValue;

        var pager = new RecordPager(input.PageSize, input.Offset, input.MaximumOffset);
        var childrenStartedByThisRun = new List<Task<ChildWorkflowHandle<RecordProcessorWorkflow>>>();

        while (true)
        {
            // After starting slidingWindowSize children, blocks until a completion signal is
            // received.
            await Workflow.WaitConditionAsync(() => trackedRecords.Count < slidingWindowSize);

            var record = await pager.NextAsync();

            // Completes the workflow if there are no more records to process.
            if (record is null)
            {
                // Awaits for all children to complete. By this point every record that was ever
                // added to trackedRecords has been removed via ReportCompletionAsync, so progress
                // is an accurate count of the records processed by this partition.
                await Workflow.WaitConditionAsync(() => trackedRecords.Count == 0);
                return progress;
            }

            // Uses ParentClosePolicy.Abandon to ensure that children survive continue-as-new of
            // the parent. Assigns a user-friendly child workflow id.
            var childOptions = new ChildWorkflowOptions
            {
                Id = $"{Workflow.Info.WorkflowId}/{record.Id}",
                ParentClosePolicy = ParentClosePolicy.Abandon,
            };

            // Starts a child workflow asynchronously, ignoring its result. The assumption is
            // that the parent doesn't need to deal with child workflow results and failures.
            // Another assumption is that a child in any situation calls the
            // ReportCompletionAsync signal.
            var childStarted = Workflow.StartChildWorkflowAsync(
                (RecordProcessorWorkflow wf) => wf.RunAsync(record),
                childOptions);
            childrenStartedByThisRun.Add(childStarted);
            trackedRecords.Add(record.Id);

            // Continues-as-new after starting pageSize children, or earlier if the server
            // suggests it (or, for ease of testing, maxHistoryLength is exceeded) because
            // history has grown large. Relying on pageSize alone is fragile: if a run also
            // accumulates a lot of other history (e.g. many completion Signals), it can outgrow
            // the suggested size before reaching pageSize children.
            if (childrenStartedByThisRun.Count == pageSize ||
                Workflow.ContinueAsNewSuggested ||
                Workflow.CurrentHistoryLength > maxHistoryLength)
            {
                // Waits for all children to start. Without this wait, workflow completion
                // through continue-as-new might lead to a situation where they never start.
                // Assumes that they never fail to start, as their automatically generated ids
                // are not expected to collide.
                await Workflow.WhenAllAsync(childrenStartedByThisRun);

                var newInput = new ProcessBatchInput
                {
                    PageSize = pageSize,
                    SlidingWindowSize = slidingWindowSize,
                    Offset = offset + childrenStartedByThisRun.Count,
                    MaximumOffset = input.MaximumOffset,
                    Progress = progress,
                    CurrentRecords = trackedRecords,
                    TestContinueAsNew = input.TestContinueAsNew,
                };

                throw Workflow.CreateContinueAsNewException(
                    (SlidingWindowBatchWorkflow wf) => wf.RunAsync(newInput));
            }
        }
    }

    /// <summary>
    /// Reports that a child workflow finished processing a record.
    /// </summary>
    /// <param name="recordId">Id of the record that finished processing.</param>
    [WorkflowSignal]
    public Task ReportCompletionAsync(int recordId)
    {
        // Handles the case when the signal is delivered before the workflow run method started.
        if (currentRecords is null)
        {
            recordsToRemove.Add(recordId);
            return Task.CompletedTask;
        }

        // Dedupes signals, as in some edge cases they can be delivered more than once.
        if (currentRecords.Remove(recordId))
        {
            progress++;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the current progress of the batch.
    /// </summary>
    [WorkflowQuery]
    public BatchProgress GetProgress() => new(progress, currentRecords ?? new HashSet<int>());

    /// <summary>
    /// Lazily loads records page by page using the <see cref="RecordLoaderActivities"/> activity.
    /// </summary>
    private sealed class RecordPager
    {
        private readonly int pageSize;
        private readonly int maximumOffset;
        private IReadOnlyList<SingleRecord> lastPage = Array.Empty<SingleRecord>();
        private int offset;
        private int index;

        public RecordPager(int pageSize, int initialOffset, int maximumOffset)
        {
            this.pageSize = pageSize;
            this.maximumOffset = maximumOffset;
            offset = initialOffset;
        }

        /// <summary>
        /// Returns the next record, loading a new page via activity if the current one is
        /// exhausted. Returns null once the maximum offset has been reached.
        /// </summary>
        public async Task<SingleRecord?> NextAsync()
        {
            if (index >= lastPage.Count)
            {
                if (offset >= maximumOffset)
                {
                    return null;
                }

                var size = Math.Min(pageSize, maximumOffset - offset);
                lastPage = await Workflow.ExecuteActivityAsync(
                    () => RecordLoaderActivities.GetRecords(size, offset),
                    new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) });
                offset += lastPage.Count;
                index = 0;

                if (lastPage.Count == 0)
                {
                    return null;
                }
            }

            return lastPage[index++];
        }
    }
}
