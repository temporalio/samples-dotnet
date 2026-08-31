namespace TemporalioSamples.Tests.NexusStandaloneActivity;

using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.NexusStandaloneActivity;
using TemporalioSamples.NexusStandaloneActivity.Handler;
using Xunit;
using Xunit.Abstractions;

public class NexusStandaloneActivityTests : TestBase
{
    public NexusStandaloneActivityTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_ActivityBackedOperation_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            DevServerOptions = new()
            {
                DownloadVersion = "v1.7.4-standalone-nexus-operations",
                ExtraArgs =
                    [
                        "--dynamic-config-value",
                        "activity.enableCallbacks=true"
                    ],
            },
        });

        var taskQueue = $"tq-{Guid.NewGuid()}";
        await env.CreateNexusEndpointAsync(NexusEndpoints.GreetingService, taskQueue);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(taskQueue).
                AddNexusService(new GreetingService()).
                AddActivity(GreetingActivities.CreateGreetingAsync));
        await worker.ExecuteAsync(async () =>
        {
            var nexusClient = env.Client.CreateNexusClient<IGreetingService>(
                NexusEndpoints.GreetingService);

            var result = await nexusClient.ExecuteNexusOperationAsync(
                svc => svc.Greet(new("Test")),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
                });
            Assert.Equal("Hello, Test!", result.Message);
        });
    }
}
