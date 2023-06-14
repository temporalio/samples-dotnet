using Temporalio.Workflows;

namespace TemporalioSamples.ActivityStickyQueues;

[Workflow]
public class FileProcessingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(int maxAttempts = 5)
    {
        var uniqueWorkerTaskQueue = await Workflow.ExecuteActivityAsync(
            (NonStickyActivities act) => act.GetUniqueTaskQueue(),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        var downloadPath = await Workflow.ExecuteActivityAsync(
            () => StickyActivities.DownloadFileToWorkerFileSystemAsync("https://temporal.io"),
            new()
            {
                TaskQueue = uniqueWorkerTaskQueue,
                // Note the use of ScheduleToCloseTimeout.
                // The reason this timeout type is used is because this task queue is unique
                // to a single worker. When that worker goes away, there won't be a way for these
                // activities to progress.
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                HeartbeatTimeout = TimeSpan.FromMinutes(1),
            });

        await Workflow.ExecuteActivityAsync(
            () => StickyActivities.WorkOnFileInWorkerFileSystemAsync(downloadPath),
            new()
            {
                TaskQueue = uniqueWorkerTaskQueue,
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                HeartbeatTimeout = TimeSpan.FromMinutes(1),
            });

        await Workflow.ExecuteActivityAsync(
            () => StickyActivities.CleanupFileFromWorkerFileSystemAsync(downloadPath),
            new()
            {
                TaskQueue = uniqueWorkerTaskQueue,
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                HeartbeatTimeout = TimeSpan.FromMinutes(1),
            });
    }
}