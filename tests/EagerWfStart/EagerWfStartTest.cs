namespace TemporalioSamples.Tests.EagerWfStart;

using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.EagerWfStart;
using Xunit;
using Xunit.Abstractions;

public class EagerWfStartTest : TestBase
{
    public EagerWfStartTest(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_EagerWorkflowStart_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        var activities = new Activities();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(activities.Greeting).
                AddWorkflow<EagerWfStartWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Start workflow with eager start enabled
            var handle = await env.Client.StartWorkflowAsync(
                (EagerWfStartWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    RequestEagerStart = true,
                });

            // Verify the workflow completes successfully
            var result = await handle.GetResultAsync();
            Assert.Equal("Hello, Temporal!", result);
        });
    }
}