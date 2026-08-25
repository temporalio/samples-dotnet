namespace TemporalioSamples.WorkflowStreams.ExternalPublisher;

using Temporalio.Extensions.WorkflowStreams;

public record HubInput(string HubId, WorkflowStreamState? StreamState = null);

public record NewsEvent(string Headline);
