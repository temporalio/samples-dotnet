namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;

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

public record OrderInput(string OrderId, WorkflowStreamState? StreamState = null);

public record PipelineInput(
    string PipelineId,
    TimeSpan? StageInterval = null,
    WorkflowStreamState? StreamState = null);

public record HubInput(string HubId, WorkflowStreamState? StreamState = null);

public record TickerInput(
    int Count = 50,
    int KeepLast = 10,
    int TruncateEvery = 5,
    TimeSpan? Interval = null,
    WorkflowStreamState? StreamState = null);

public record LlmInput(
    string Prompt,
    string Model = "gpt-4o-mini",
    WorkflowStreamState? StreamState = null);

public record StatusEvent(string Kind, string OrderId);

public record ProgressEvent(string Message);

public record StageEvent(string Stage);

public record NewsEvent(string Headline);

public record TickEvent(int N);

public record TextDelta(string Text);

public record TextComplete(string FullText);

public record RetryEvent(int Attempt);
