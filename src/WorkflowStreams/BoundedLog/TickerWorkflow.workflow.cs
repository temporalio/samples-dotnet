namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class TickerWorkflow
{
    private readonly WorkflowStream stream;

    [WorkflowInit]
    public TickerWorkflow(TickerInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(TickerInput input)
    {
        var tick = stream.Topic(WorkflowStreamsConstants.TopicTick);
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

        await Workflow.DelayAsync(WorkflowStreamsConstants.DrainDelay);
        return $"ticker emitted {input.Count} events";
    }
}
