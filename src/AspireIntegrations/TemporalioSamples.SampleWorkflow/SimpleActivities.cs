using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.SampleWorkflow;

public class SimpleActivities
{
    [Activity]
    public async Task DoSomethingAsync()
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Doing something async!");
        await Task.Delay(TimeSpan.FromSeconds(2));
        ActivityExecutionContext.Current.Logger.LogInformation("Done something async!");
    }
}