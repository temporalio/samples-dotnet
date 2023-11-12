using Temporalio.Activities;

namespace TemporalioSamples.ActivityStickyQueues;

public class NormalActivities
{
    private readonly string uniqueWorkerTaskQueue;

    public NormalActivities(string uniqueWorkerTaskQueue) =>
        this.uniqueWorkerTaskQueue = uniqueWorkerTaskQueue;

    [Activity]
#pragma warning disable CA1024
    public string GetUniqueTaskQueue() => uniqueWorkerTaskQueue;
#pragma warning restore CA1024
}