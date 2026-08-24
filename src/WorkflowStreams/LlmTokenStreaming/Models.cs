namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;

public record LlmInput(
    string Prompt,
    string Model = "gpt-4o-mini",
    WorkflowStreamState? StreamState = null);

public record TextDelta(string Text);

public record TextComplete(string FullText);

public record RetryEvent(int Attempt);
