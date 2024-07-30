using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.OpenTelemetry.Common;

public static class Activities
{
    [Activity]
    public static void MyActivity(string input)
    {
        ActivityExecutionContext.Current.Logger.LogInformation("Executing activity for OpenTelemetry sample.");

        ActivityExecutionContext.Current.MetricMeter.CreateCounter<int>("my-activity-counter", description: "Counter used to instrument an activity.").Add(123);
    }
}
