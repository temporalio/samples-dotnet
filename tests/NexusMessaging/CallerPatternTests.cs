namespace TemporalioSamples.Tests.NexusMessaging;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.CallerPattern.Caller;
using TemporalioSamples.NexusMessaging.CallerPattern.Handler;
using TemporalioSamples.NexusMessaging.Common;
using Xunit;
using Xunit.Abstractions;

public class CallerPatternTests : TestBase
{
    public CallerPatternTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_CallerWorkflow_Succeeds()
    {
        // SetLanguage is backed by a Workflow Update, which needs a dev server build that supports
        // update callbacks.
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
                ],
            },
        });

        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await env.CreateNexusEndpointAsync(NexusEndpoints.GreetingService, handlerTaskQueue);
        var userId = $"user-{Guid.NewGuid()}";
        var workflowId = $"GreetingWorkflow_for_{userId}";

        // Start entity workflow
        await env.Client.StartWorkflowAsync(
            (GreetingWorkflow wf) => wf.RunAsync(userId),
            new(id: workflowId, taskQueue: handlerTaskQueue)
            {
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
            });

        // Run handler worker
        using var handlerWorker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new NexusGreetingService()).
                AddWorkflow<GreetingWorkflow>().
                AddAllActivities(new GreetingActivities()));
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                env.Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<CallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                var result = await env.Client.ExecuteWorkflowAsync(
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
