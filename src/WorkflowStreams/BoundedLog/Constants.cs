namespace TemporalioSamples.WorkflowStreams.BoundedLog;

public static class Constants
{
    public const string TaskQueue = "workflow-streams-bounded-log";

    public const string TopicTick = "tick";

    public static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(500);
}
