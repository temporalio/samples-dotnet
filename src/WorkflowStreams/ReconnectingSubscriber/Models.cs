namespace TemporalioSamples.WorkflowStreams.ReconnectingSubscriber;

using Temporalio.Extensions.WorkflowStreams;

public record PipelineInput(
    string PipelineId,
    TimeSpan? StageInterval = null,
    WorkflowStreamState? StreamState = null);

public record StageEvent(string Stage);
