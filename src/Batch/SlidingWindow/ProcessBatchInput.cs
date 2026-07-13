namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Input of <see cref="SlidingWindowBatchWorkflow.RunAsync"/>.
/// </summary>
public record ProcessBatchInput
{
    /// <summary>
    /// Gets the number of records to load in a single <see cref="RecordLoaderActivities"/> call.
    /// </summary>
    required public int PageSize { get; init; }

    /// <summary>
    /// Gets the number of parallel record processing child workflows to run.
    /// </summary>
    required public int SlidingWindowSize { get; init; }

    /// <summary>
    /// Gets the index of the first record to process. 0 to start the batch processing.
    /// </summary>
    required public int Offset { get; init; }

    /// <summary>
    /// Gets the maximum offset (exclusive) to process by this workflow.
    /// </summary>
    required public int MaximumOffset { get; init; }

    /// <summary>
    /// Gets the total number of records processed so far by this workflow.
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Gets the ids of records that are currently being processed by child workflows.
    ///
    /// <para>This puts a limit on the sliding window size, as workflow arguments cannot exceed
    /// 2MB in JSON format. Another practical limit is the number of signals a workflow can
    /// handle per second. Adjust the number of partitions to keep this rate at a reasonable
    /// value.</para>
    /// </summary>
    public HashSet<int> CurrentRecords { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether continue-as-new should be forced well before the real
    /// history-size threshold would suggest it, so tests can deterministically exercise that
    /// code path without generating a large amount of real history.
    /// </summary>
    public bool TestContinueAsNew { get; init; }
}
