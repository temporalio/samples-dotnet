namespace TemporalioSamples.Tests.WorkerSpecificTaskQueues;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.WorkerSpecificTaskQueues;
using Xunit;
using Xunit.Abstractions;

public class FileProcessingWorkflowTests : WorkflowEnvironmentTestBase
{
    public FileProcessingWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_SimpleRun_SucceedsAfterRetry()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";

        // Mock activities
        [Activity("GetUniqueTaskQueue")]
        string MockGetUniqueTaskQueue() => taskQueue;

        [Activity("DownloadFileToWorkerFileSystem")]
        string MockDownloadFileToWorkerFileSystem() => "/path/to/file";

        // We want this to fail the first two times
        var timesCalled = 0;
        [Activity("WorkOnFileInWorkerFileSystem")]
        void MockWorkOnFileInWorkerFileSystem(string path)
        {
            timesCalled++;
            if (timesCalled < 3)
            {
                throw new InvalidOperationException("Intentional failure");
            }
        }

        [Activity("CleanupFileFromWorkerFileSystem")]
        void MockCleanupFileFromWorkerFileSystem(string path)
        {
        }

        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(taskQueue).
                AddActivity(MockGetUniqueTaskQueue).
                AddActivity(MockDownloadFileToWorkerFileSystem).
                AddActivity(MockWorkOnFileInWorkerFileSystem).
                AddActivity(MockCleanupFileFromWorkerFileSystem).
                AddWorkflow<FileProcessingWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Just run it to make sure it completes and confirm activity call count is 3
            await Client.ExecuteWorkflowAsync(
                (FileProcessingWorkflow wf) => wf.RunAsync(5),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: taskQueue));
            Assert.Equal(3, timesCalled);
        });
    }
}