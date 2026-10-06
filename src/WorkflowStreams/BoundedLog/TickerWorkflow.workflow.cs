namespace TemporalioSamples.WorkflowStreams.BoundedLog;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class TickerWorkflow
{
    private readonly WorkflowStream stream;
    private readonly SortedSet<string> completedSubscribers = new(StringComparer.Ordinal);

    [WorkflowInit]
    public TickerWorkflow(TickerInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(TickerInput input)
    {
        var tick = stream.GetTopic<TickEvent>(Constants.TopicTick);
        var interval = input.Interval ?? TimeSpan.FromMilliseconds(200);

        for (var n = 0; n < input.Count; n++)
        {
            tick.Publish(new TickEvent(n));
            if (interval > TimeSpan.Zero)
            {
                await Workflow.DelayAsync(interval);
            }

            var published = n + 1;
            if (published % input.TruncateEvery == 0 && published > input.KeepLast)
            {
                stream.Truncate(published - input.KeepLast);
            }
        }

        // Keep poll Updates available until all expected subscribers acknowledge the final
        // tick. The timeout lets the Workflow finish if a subscriber never connects or fails.
        await Workflow.WaitConditionAsync(
            () => completedSubscribers.Count >= input.ExpectedSubscribers,
            Constants.SubscriberTimeout);
        return $"ticker emitted {input.Count} events";
    }

    [WorkflowSignal]
    public async Task SubscriberCompleteAsync(string subscriberId) =>
        completedSubscribers.Add(subscriberId);
}
