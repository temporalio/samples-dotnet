namespace TemporalioSamples.WorkflowStreams.ExternalPublisher;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-external-publisher";

    public const string TopicNews = "news";

    public const string DoneHeadline = "-- end of feed --";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
