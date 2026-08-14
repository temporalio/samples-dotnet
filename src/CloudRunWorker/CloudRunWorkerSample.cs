namespace TemporalioSamples.CloudRunWorker;

using Temporalio.Worker;

/// <summary>
/// Shared worker configuration so both the entrypoint and the tests register the same
/// workflow and activities.
/// </summary>
public static class CloudRunWorkerSample
{
    /// <summary>
    /// Register the sample workflow and activities on the given worker options.
    /// </summary>
    /// <param name="options">Worker options to configure.</param>
    /// <returns>The same options, for chaining.</returns>
    public static TemporalWorkerOptions ConfigureOptions(TemporalWorkerOptions options) =>
        options.
            AddWorkflow<GreetingWorkflow>().
            AddActivity(GreetingActivities.SayHello);
}
