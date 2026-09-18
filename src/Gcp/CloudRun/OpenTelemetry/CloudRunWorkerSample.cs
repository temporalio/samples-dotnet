namespace TemporalioSamples.Gcp.CloudRun.OpenTelemetry;

using Temporalio.Worker;

// Shared so the entrypoint and tests register the same workflow and activities.
public static class CloudRunWorkerSample
{
    public static TemporalWorkerOptions ConfigureOptions(TemporalWorkerOptions options) =>
        options.
            AddWorkflow<GreetingWorkflow>().
            AddActivity(GreetingActivities.SayHello);
}
