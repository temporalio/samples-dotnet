namespace TemporalioSamples.WorkflowStreams.ConcurrentSubscriptions;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class OrderWorkflow
{
    private readonly WorkflowStream stream;

    [WorkflowInit]
    public OrderWorkflow(OrderInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(OrderInput input)
    {
        var status = stream.GetTopic<StatusEvent>(Constants.TopicStatus);
        var progress = stream.GetTopic<ProgressEvent>(Constants.TopicProgress);

        status.Publish(new StatusEvent("received", input.OrderId));
        var chargeId = await Workflow.ExecuteActivityAsync(
            () => PaymentActivities.ChargeCardAsync(input.OrderId),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), });
        status.Publish(new StatusEvent("shipped", input.OrderId));
        progress.Publish(new ProgressEvent($"charge id: {chargeId}"));
        status.Publish(new StatusEvent("complete", input.OrderId));

        // Poll Updates cannot read the stream after this Workflow closes. This delay is a
        // stopgap that gives active subscribers time to fetch the final events; it does not
        // guarantee delivery to slow subscribers. Use subscriber acknowledgements when needed.
        await Workflow.DelayAsync(Constants.DrainDelay);
        return chargeId;
    }
}
