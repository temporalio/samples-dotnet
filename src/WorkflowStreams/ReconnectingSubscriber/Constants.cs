namespace TemporalioSamples.WorkflowStreams.ReconnectingSubscriber;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-reconnecting-subscriber";

    public const string TopicStatus = "status";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
