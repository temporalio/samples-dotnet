namespace TemporalioSamples.Tests.NexusMessaging;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Caller;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;
using Xunit;
using Xunit.Abstractions;

public class OnDemandPatternTests : WorkflowEnvironmentTestBase
{
    public OnDemandPatternTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_CallerRemoteWorkflow_Succeeds()
    {
        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(NexusEndpoints.RemoteGreetingService, handlerTaskQueue);

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

                Assert.Contains("Started remote workflow for user: user-one", result[0]);
                Assert.Contains("Started remote workflow for user: user-two", result[1]);
                Assert.Contains("[One] Supported languages:", result[2]);
                Assert.Contains($"[One] Set language from {Language.English} to {Language.Spanish}", result[3]);
                Assert.Contains("[One] Approved", result[4]);
                Assert.Contains("[Two] Current language:", result[5]);
                Assert.Contains($"[Two] Set language from {Language.English} to {Language.French}", result[6]);
                Assert.Contains("[Two] Approved", result[7]);
                Assert.Contains("[One] Result:", result[8]);
                Assert.Contains("[Two] Result:", result[9]);
            });
        });
    }
}
