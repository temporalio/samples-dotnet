namespace TemporalioSamples.Mutex;

using Temporalio.Activities;

public record NotifyLockedInput(string ResourceId, string ReleaseSignalName);

public record UseApiThatCantBeCalledInParallelInput(TimeSpan SleepFor);

public record NotifyUnlockedInput(string ResourceId);

public static class Activities
{
    [Activity]
    public static void NotifyLocked(NotifyLockedInput input)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Lock for resource '{ResourceId}' acquired, release signal name '{ReleaseSignalName}'", input.ResourceId, input.ReleaseSignalName);
    }

    [Activity]
    public static async Task UseApiThatCantBeCalledInParallelAsync(UseApiThatCantBeCalledInParallelInput input)
    {
        var logger = ActivityExecutionContext.Current.Logger;

        logger.LogInformation("Sleeping for '{SleepFor}'...", input.SleepFor);

        await Task.Delay(input.SleepFor);

        logger.LogInformation("Done sleeping!");
    }

    [Activity]
    public static void NotifyUnlocked(NotifyUnlockedInput input)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Lock for resource '{ResourceId}' released", input.ResourceId);
    }
}
