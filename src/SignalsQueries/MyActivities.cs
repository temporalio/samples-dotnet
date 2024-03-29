namespace TemporalioSamples.SignalsQueries;

using Temporalio.Activities;

public class MyActivities
{
    [Activity]
    public static void SendCoupon(string? userId)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Sending coupon to user {UserId}", userId);
    }
}