namespace TemporalioSamples.WorkflowStreams;

public static class WorkflowStreamsConstants
{
    public const string TaskQueue = "workflow-streams";

    public const string LlmTaskQueue = "workflow-streams-llm";

    public const string TopicStatus = "status";

    public const string TopicProgress = "progress";

    public const string TopicNews = "news";

    public const string TopicTick = "tick";

    public const string TopicDelta = "delta";

    public const string TopicComplete = "complete";

    public const string TopicRetry = "retry";

    public const string DoneHeadline = "-- end of feed --";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
