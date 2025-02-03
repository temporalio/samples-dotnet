namespace TemporalioSamples.SleepForDays;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class Activities
{
    // Stub for an actual implementation for sending emails.
    [Activity]
    public void SendEmail(string msg)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("{Msg}", msg);
    }
}