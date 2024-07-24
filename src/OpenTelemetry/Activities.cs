using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.OpenTelemetry
{
    public static class Activities
    {
        [Activity]
        public static void MyActivity(string input)
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Executing activity for OpenTelemetry sample.");
        }
    }
}
