using Temporalio.Exceptions;

namespace TemporalioSamples.Tests.WorkflowUpdate;

using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class WorkflowUpdateTests : TestBase
{
    public WorkflowUpdateTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task SimpleRun_Succeed()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddWorkflow<TemporalioSamples.WorkflowUpdate.WorkflowUpdate>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (TemporalioSamples.WorkflowUpdate.WorkflowUpdate wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            Assert.Equal(
                TemporalioSamples.WorkflowUpdate.WorkflowUpdate.ScreenId.Screen2,
                await handle.ExecuteUpdateAsync((wf) => wf.SubmitScreenAsync(new TemporalioSamples.WorkflowUpdate.WorkflowUpdate.UiRequest($"requestId-{Guid.NewGuid()}", TemporalioSamples.WorkflowUpdate.WorkflowUpdate.ScreenId.Screen1))));

            Assert.Equal(
                TemporalioSamples.WorkflowUpdate.WorkflowUpdate.ScreenId.End,
                await handle.ExecuteUpdateAsync((wf) => wf.SubmitScreenAsync(new TemporalioSamples.WorkflowUpdate.WorkflowUpdate.UiRequest($"requestId-{Guid.NewGuid()}", TemporalioSamples.WorkflowUpdate.WorkflowUpdate.ScreenId.Screen2))));

            // Workflow completes
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Reject_Update()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddWorkflow<TemporalioSamples.WorkflowUpdate.WorkflowUpdate>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (TemporalioSamples.WorkflowUpdate.WorkflowUpdate wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync(wf => wf.SubmitScreenAsync(null!)));
        });
    }
}
