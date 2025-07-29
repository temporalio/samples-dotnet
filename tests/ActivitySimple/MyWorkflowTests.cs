namespace TemporalioSamples.Tests.ActivitySimple;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.ActivitySimple;
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
        var myActivities = new MyActivities();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(myActivities.SelectFromDatabaseAsync).
                AddActivity(MyActivities.DoStaticThing).
                AddWorkflow<MyWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Just run the workflow and confirm output
            var result = await env.Client.ExecuteWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(false),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("some-static-value", result);
        });
    }

    [Fact]
    public async Task RunAsync_SimpleRun_FailsAsExpected()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var myActivities = new MyActivities();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(myActivities.SelectFromDatabaseAsync).
                AddActivity(MyActivities.DoStaticThing).
                AddWorkflow<MyWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var opts = new WorkflowOptions
            {
                Id = $"workflow-{Guid.NewGuid()}",
                TaskQueue = worker.Options.TaskQueue!,
                RetryPolicy = new()
                {
                    MaximumAttempts = 1,
                },
            };
            // Just run the workflow and confirm output
            var result = await env.Client.ExecuteWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(true), opts);
            Assert.Equal("some-static-value", result);
        });
    }
}