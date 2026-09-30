namespace TemporalioSamples.WorkflowStreams.ConcurrentSubscriptions;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-concurrent-subscriptions";

    public const string TopicStatus = "status";

    public const string TopicProgress = "progress";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
