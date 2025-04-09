using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.WorkerSpecificTaskQueues;

[Workflow]
public class FileProcessingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(int maxAttempts)
    {
        // When using a worker-specific task queue, if a failure occurs, we want to retry all of the
        // worker-specific logic, so wrap all the logic here in a loop.
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                await ProcessFileAsync();
                return;
            }
            catch (Exception e)
            {
                // If it's at max attempts, re-throw to fail the workflow
                if (attempt >= maxAttempts)
                {
                    Workflow.Logger.LogError(
                        e,
                        "File processing failed and reached {Attempt} attempts, failing workflow",
                        attempt);
                    throw;
                }
                // Otherwise, just warn and continue
                Workflow.Logger.LogWarning(
                    e,
                    "File processing failed on attempt {Attempt}, trying again",
                    attempt);
            }
        }
    }

    private async Task ProcessFileAsync()
    {
        var uniqueWorkerTaskQueue = await Workflow.ExecuteActivityAsync(
            (NormalActivities act) => act.GetUniqueTaskQueue(),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        var downloadPath = await Workflow.ExecuteActivityAsync(
            () => WorkerSpecificActivities.DownloadFileToWorkerFileSystemAsync("https://temporal.io"),
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
            () => WorkerSpecificActivities.WorkOnFileInWorkerFileSystemAsync(downloadPath),
            new()
            {
                TaskQueue = uniqueWorkerTaskQueue,
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                HeartbeatTimeout = TimeSpan.FromMinutes(1),
            });

        await Workflow.ExecuteActivityAsync(
            () => WorkerSpecificActivities.CleanupFileFromWorkerFileSystemAsync(downloadPath),
            new()
            {
                TaskQueue = uniqueWorkerTaskQueue,
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                HeartbeatTimeout = TimeSpan.FromMinutes(1),
            });
    }
}