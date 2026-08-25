namespace TemporalioSamples.WorkflowStreams.ListenerSubscription;

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
        var status = stream.Topic(Constants.TopicStatus);
        var progress = stream.Topic(Constants.TopicProgress);

        status.Publish(new StatusEvent("received", input.OrderId));
        var chargeId = await Workflow.ExecuteActivityAsync(
            () => PaymentActivities.ChargeCardAsync(input.OrderId),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), });
        status.Publish(new StatusEvent("shipped", input.OrderId));
        progress.Publish(new ProgressEvent($"charge id: {chargeId}"));
        status.Publish(new StatusEvent("complete", input.OrderId));

        await Workflow.DelayAsync(Constants.DrainDelay);
        return chargeId;
    }
}
