namespace TemporalioSamples.Gcp.CloudRun.Id;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public static class Activities
{
    [Activity]
    public static string SayHello(string name)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("SayHello activity: {Name}", name);
        return $"Hello, {name}!";
    }
}
