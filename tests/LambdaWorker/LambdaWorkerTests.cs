namespace TemporalioSamples.Tests.LambdaWorker;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.LambdaWorker.Worker;
using Xunit;
using Xunit.Abstractions;

public class LambdaWorkerTests : TestBase
{
    public LambdaWorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            LambdaWorkerSample.ConfigureWorkerOptions(
                new TemporalWorkerOptions("lambda-worker-test-task-queue")));
        await worker.ExecuteAsync(async () =>
        {
            var result = await env.Client.ExecuteWorkflowAsync(
                (SampleWorkflow wf) => wf.RunAsync("Serverless Lambda Worker!"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Serverless Lambda Worker!!", result);
        });
    }
}
