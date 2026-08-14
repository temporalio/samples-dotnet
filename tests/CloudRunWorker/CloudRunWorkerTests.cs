namespace TemporalioSamples.Tests.CloudRunWorker;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.CloudRunWorker;
using Xunit;
using Xunit.Abstractions;

public class CloudRunWorkerTests : TestBase
{
    public CloudRunWorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task GreetingWorkflow_SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            CloudRunWorkerSample.ConfigureOptions(
                new TemporalWorkerOptions("cloud-run-worker-test-task-queue")));
        await worker.ExecuteAsync(async () =>
        {
            var result = await env.Client.ExecuteWorkflowAsync(
                (GreetingWorkflow wf) => wf.RunAsync("Cloud Run"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Cloud Run!", result);
        });
    }
}
