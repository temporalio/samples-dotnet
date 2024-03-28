namespace TemporalioSamples.SignalsQueries;

using Temporalio.Activities;

public class MyActivities
{
    [Activity]
    public static string SendCoupon(string? userId) => "coupon emailed";
}