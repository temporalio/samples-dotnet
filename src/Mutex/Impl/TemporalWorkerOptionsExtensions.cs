namespace TemporalioSamples.Mutex.Impl;

using Temporalio.Client;
using Temporalio.Worker;

public static class TemporalWorkerOptionsExtensions
{
    public static TemporalWorkerOptions AddWorkflowMutex(this TemporalWorkerOptions options, ITemporalClient client)
    {
        var mutexActivities = new MutexActivities(client);

        options
            .AddAllActivities(mutexActivities)
            .AddWorkflow<MutexWorkflow>();

        return options;
    }
}
