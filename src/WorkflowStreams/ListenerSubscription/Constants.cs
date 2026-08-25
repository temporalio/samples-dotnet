namespace TemporalioSamples.WorkflowStreams.ListenerSubscription;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-listener-subscription";

    public const string TopicStatus = "status";

    public const string TopicProgress = "progress";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
