namespace TemporalioSamples.SleepForDays;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class SleepForDaysActivities
{
    // Stub for an actual implementation for sending emails.
    [Activity]
    public void SendEmail(string msg)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("{Msg}", msg);
    }
}