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
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddWorkflow<MyWorkflowUpdate>());
        await worker.ExecuteAsync(async () =>
        {
            // Just run the workflow and confirm output
            Console.WriteLine("0: ");

            var handle = await env.Client.StartWorkflowAsync(
                (MyWorkflowUpdate wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            Console.WriteLine("1: ");

            await handle.ExecuteUpdateAsync(
                (MyWorkflowUpdate wf) => wf.AddValueAsync(1));

            Console.WriteLine("2: ");

            Assert.Equal(1, await handle.GetResultAsync());
        });
    }
}