namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;

public record OrderInput(string OrderId, WorkflowStreamState? StreamState = null);

public record StatusEvent(string Kind, string OrderId);

public record ProgressEvent(string Message);
