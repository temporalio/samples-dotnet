namespace TemporalioSamples.SignalsQueries;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class MyActivities
{
    [Activity]
    public static void SendCoupon(string? userId)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Sending coupon to user {UserId}", userId);
    }
}