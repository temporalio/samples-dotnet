namespace TemporalioSamples.UpdateWithStartEarlyReturn;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class Activities
{
    [Activity]
    public static async Task AuthorizePaymentAsync(int amount)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Authorizing payment with ID {PaymentId} for amount {Amount}",
            ActivityExecutionContext.Current.Info.WorkflowId,
            amount);
        // Simulate some time taken
        await Task.Delay(1000);
    }

    [Activity]
    public static async Task CapturePaymentAsync()
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Capturing payment with ID {PaymentId}",
            ActivityExecutionContext.Current.Info.WorkflowId);
        // Simulate some time taken
        await Task.Delay(2000);
    }
}