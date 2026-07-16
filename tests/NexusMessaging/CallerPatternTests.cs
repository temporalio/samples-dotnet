namespace TemporalioSamples.Tests.NexusMessaging;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.CallerPattern;
using TemporalioSamples.NexusMessaging.CallerPattern.Caller;
using TemporalioSamples.NexusMessaging.CallerPattern.Handler;
using TemporalioSamples.NexusMessaging.Common;
using Xunit;
using Xunit.Abstractions;

public class CallerPatternTests : WorkflowEnvironmentTestBase
{
    public CallerPatternTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_CallerWorkflow_Succeeds()
    {
        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(NexusEndpoints.GreetingService, handlerTaskQueue);
        var userId = $"user-{Guid.NewGuid()}";
        var workflowId = $"GreetingWorkflow_for_{userId}";

        // Start entity workflow
        await Client.StartWorkflowAsync(
            (GreetingWorkflow wf) => wf.RunAsync(userId),
            new(id: workflowId, taskQueue: handlerTaskQueue)
            {
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
            });

        // Run handler worker
        using var handlerWorker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new NexusGreetingService()).
                AddWorkflow<GreetingWorkflow>().
                AddAllActivities(new GreetingActivities()));
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<CallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                var result = await Client.ExecuteWorkflowAsync(
                    (CallerWorkflow wf) => wf.RunAsync(userId),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));

                Assert.Equal(4, result.Length);
                Assert.Contains("Supported languages:", result[0]);
                Assert.Contains("Current language:", result[1]);
                Assert.Contains($"Set language from {Language.English} to {Language.Chinese}", result[2]);
                Assert.Equal("Approved workflow", result[3]);
            });
        });
    }
}
