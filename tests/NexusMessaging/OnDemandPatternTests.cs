namespace TemporalioSamples.Tests.NexusMessaging;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Caller;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;
using Xunit;
using Xunit.Abstractions;

public class OnDemandPatternTests : WorkflowEnvironmentTestBase
{
    private static Task<string>? lazyHandlerTaskQueue;

    public OnDemandPatternTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public Task<string> EnsureHandlerTaskQueueAsync() =>
        LazyInitializer.EnsureInitialized(ref lazyHandlerTaskQueue, async () =>
        {
            var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
            await Env.TestEnv.CreateNexusEndpointAsync(INexusRemoteGreetingService.EndpointName, handlerTaskQueue);
            return handlerTaskQueue;
        });

    [Fact]
    public async Task RunAsync_CallerRemoteWorkflow_Succeeds()
    {
        var handlerTaskQueue = await EnsureHandlerTaskQueueAsync();

        // Run handler worker
        using var handlerWorker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new NexusRemoteGreetingService()).
                AddWorkflow<GreetingWorkflow>().
                AddAllActivities(new GreetingActivities()));
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<CallerRemoteWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                var result = await Client.ExecuteWorkflowAsync(
                    (CallerRemoteWorkflow wf) => wf.RunAsync(),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));

                Assert.True(result.Length > 0);
                // Both workflows should have been started and approved
                Assert.Contains(result, r => r.Contains("Started remote workflow"));
                Assert.Contains(result, r => r.Contains("[One] Result:"));
                Assert.Contains(result, r => r.Contains("[Two] Result:"));
            });
        });
    }
}
