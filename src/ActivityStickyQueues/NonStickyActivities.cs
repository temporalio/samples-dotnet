using Temporalio.Activities;

namespace TemporalioSamples.ActivityStickyQueues;

public class NonStickyActivities
{
    private readonly string uniqueWorkerTaskQueue;

    public NonStickyActivities(string uniqueWorkerTaskQueue)
    {
        this.uniqueWorkerTaskQueue = uniqueWorkerTaskQueue;
    }

    [Activity]
    public Task<string> GetUniqueTaskQueueAsync()
    {
        return Task.FromResult(uniqueWorkerTaskQueue);
    }
}