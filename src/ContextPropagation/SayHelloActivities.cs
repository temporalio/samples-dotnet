namespace TemporalioSamples.ContextPropagation;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;

public class SayHelloActivities
{
    [Activity]
    public string SayHello(string name)
    {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Activity called by user {UserId}", MyContext.UserId);
        return $"Hello, {name}!";
    }
}