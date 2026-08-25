namespace TemporalioSamples.CloudRunWorker;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class Activities
{
    [Activity]
    public static string SayHello(string name)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "SayHello activity invoked with name: {Name}",
            name);
        return $"Hello, {name}!";
    }
}
