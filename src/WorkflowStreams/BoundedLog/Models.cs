namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;

public record TickerInput(
    int Count = 50,
    int KeepLast = 10,
    int TruncateEvery = 5,
    TimeSpan? Interval = null,
    WorkflowStreamState? StreamState = null);

public record TickEvent(int N);
