using TemporalioSamples.WorkflowUpdate;

namespace TemporalioSamples.Tests.WorkflowUpdate;

using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class MyWorkflowTests : TestBase
{
    public MyWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddWorkflow<MyWorkflowUpdate>());
        await worker.ExecuteAsync(async () =>
        {
            // Just run the workflow and confirm output
            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflowUpdate wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            await handle.SignalAsync(
                (MyWorkflowUpdate wf) => wf.ExitAsync());

            Assert.Equal(0, await handle.GetResultAsync());
        });
    }
}