namespace TemporalioSamples.WorkflowStreams.BasicPublishSubscribe;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-basic-publish-subscribe";

    public const string TopicStatus = "status";

    public const string TopicProgress = "progress";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
