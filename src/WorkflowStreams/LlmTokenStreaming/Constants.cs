namespace TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-llm-token-streaming";

    public const string TopicDelta = "delta";

    public const string TopicComplete = "complete";

    public const string TopicRetry = "retry";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
