namespace TemporalioSamples.Tests.EagerWorkflowStart;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.EagerWorkflowStart;
using Xunit;
using Xunit.Abstractions;

public class EagerWorkflowStartTest : WorkflowEnvironmentTestBase
{
    public EagerWorkflowStartTest(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_EagerWorkflowStart_Succeeds()
    {
        var activities = new Activities();
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(activities.Greeting).
                AddWorkflow<EagerWorkflowStartWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Start workflow with eager start enabled
            var handle = await Client.StartWorkflowAsync(
                (EagerWorkflowStartWorkflow wf) => wf.RunAsync("Temporal"),
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