namespace TemporalioSamples.Timer;

using Temporalio.Activities;

public class MyActivities
{
    [Activity]
    public static string Charge(string userId) => "charge successful";
}