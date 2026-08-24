namespace TemporalioSamples.WorkflowStreams;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Extensions.WorkflowStreams;

public static class PaymentActivities
{
    [Activity]
    public static async Task<string> ChargeCardAsync(string orderId)
    {
        await using var streamClient = WorkflowStreamClient.FromActivity(
            new() { BatchInterval = TimeSpan.FromMilliseconds(200), });
        var progress = streamClient.Topic(WorkflowStreamsConstants.TopicProgress);

        progress.Publish(new ProgressEvent("charging card..."));
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Charging card for order {OrderId}", orderId);
        await Task.Delay(
            TimeSpan.FromSeconds(1),
            ActivityExecutionContext.Current.CancellationToken);
        progress.Publish(new ProgressEvent("card charged"));
        await streamClient.FlushAsync();
        return $"charge-{orderId}";
    }
}
