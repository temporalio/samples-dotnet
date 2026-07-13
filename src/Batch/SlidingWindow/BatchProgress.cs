namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Result of the <see cref="SlidingWindowBatchWorkflow.GetProgress"/> query.
/// </summary>
/// <param name="Progress">Count of completed record processing child workflows.</param>
/// <param name="CurrentRecords">Ids of records that are currently being processed by child
/// workflows.</param>
public record BatchProgress(int Progress, IReadOnlySet<int> CurrentRecords);
