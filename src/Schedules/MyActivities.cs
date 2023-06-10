using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.Schedules;

public static class MyActivities
{
    [Activity]
    public static void AddReminderToDatabase(string text)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Adding reminder record to the database");
    }

    [Activity]
    public static Task NotifyUserAsync(string text)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Notifying user Reminder: {text}", text);
        return Task.CompletedTask;
    }
}