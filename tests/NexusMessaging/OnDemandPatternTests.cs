namespace TemporalioSamples.Tests.NexusMessaging;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Caller;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;
using Xunit;
using Xunit.Abstractions;

public class OnDemandPatternTests : TestBase
{
    public OnDemandPatternTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_CallerRemoteWorkflow_Succeeds()
    {
        // SetLanguage is backed by a Workflow Update and AttachApprovalContext by
        // Signal-with-Start, both of which need a dev server build that supports them.
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            DevServerOptions = new()
            {
                DownloadVersion = "v1.7.4-standalone-nexus-operations",
                ExtraArgs =
                [
                    "--dynamic-config-value",
                    "history.enableUpdateCallbacks=true",
                    "--dynamic-config-value",
                    "history.enableCHASMSignalBacklinks=true",
                    "--dynamic-config-value",
                    "history.enableSignalWithStartFromWorkflow=true",
                ],
            },
        });

        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await env.CreateNexusEndpointAsync(NexusEndpoints.RemoteGreetingService, handlerTaskQueue);

        // Run handler worker
        using var handlerWorker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new NexusRemoteGreetingService()).
                AddWorkflow<GreetingWorkflow>().
                AddAllActivities(new GreetingActivities()));
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                env.Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<CallerRemoteWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                var result = await env.Client.ExecuteWorkflowAsync(
                    (CallerRemoteWorkflow wf) => wf.RunAsync(),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));

                Assert.Contains("Attached approval context before the workflow existed: user-one", result[0]);
                Assert.Contains("Started remote workflow for user: user-one", result[1]);
                Assert.Contains("Started remote workflow for user: user-two", result[2]);
                Assert.Contains("Attached approval context to the running workflow: user-two", result[3]);
                Assert.Contains("[One] Supported languages:", result[4]);
                Assert.Contains($"[One] Set language from {Language.English} to {Language.Spanish}", result[5]);
                Assert.Contains("[One] Approved", result[6]);
                Assert.Contains("[Two] Current language:", result[7]);
                Assert.Contains($"[Two] Set language from {Language.English} to {Language.French}", result[8]);
                Assert.Contains("[Two] Approved", result[9]);
                Assert.Contains("[One] Result:", result[10]);
                Assert.Contains("[Two] Result:", result[11]);
            });
        });
    }
}
