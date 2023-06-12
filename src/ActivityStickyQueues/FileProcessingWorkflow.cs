using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.ActivityStickyQueues;

[Workflow]
public class FileProcessingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(int maxAttempts = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            try
            {
                var uniqueWorkerTaskQueue = await Workflow.ExecuteActivityAsync(
                    (NonStickyActivities act) => act.GetUniqueTaskQueueAsync(),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(1),
                    });

                var downloadPath = Path.GetTempFileName();

                await Workflow.ExecuteActivityAsync(
                    (StickyActivities act) => act.DownloadFileToWorkerFileSystemAsync("https://temporal.io", downloadPath),
                    new()
                    {
                        TaskQueue = uniqueWorkerTaskQueue,
                        // Note the use of ScheduleToCloseTimeout.
                        // The reason this timeout type is used is because this task queue is unique
                        // to a single worker. When that worker goes away, there won't be a way for these
                        // activities to progress.
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                    });
                try
                {
                    await Workflow.ExecuteActivityAsync(
                        (StickyActivities act) => act.WorkOnFileInWorkerFileSystemAsync(downloadPath),
                        new()
                        {
                            TaskQueue = uniqueWorkerTaskQueue,
                            ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                        });
                }
                finally
                {
                    await Workflow.ExecuteActivityAsync(
                        (StickyActivities act) => act.CleanupFileFromWorkerFileSystemAsync(downloadPath),
                        new()
                        {
                            TaskQueue = uniqueWorkerTaskQueue,
                            ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                        });
                }

                return;
            }
            catch (Exception)
            {
                if (attempt == maxAttempts)
                {
                    Workflow.Logger.LogInformation("Final attempt ({attempt}) failed, giving up", attempt);
                    throw;
                }

                Workflow.Logger.LogInformation("Attempt {attempt} failed, retrying on a new Worker", attempt);
            }
        }
    }
}