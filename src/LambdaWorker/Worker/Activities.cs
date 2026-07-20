namespace TemporalioSamples.LambdaWorker.Worker;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class Activities
{
    [Activity]
    public static string HelloActivity(string name)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "HelloActivity started with name: {Name}",
            name);
        return $"Hello, {name}!";
    }
}
