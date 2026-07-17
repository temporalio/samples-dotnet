namespace TemporalioSamples.Tests.NexusStandaloneOperations;

using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.NexusStandaloneOperations;
using TemporalioSamples.NexusStandaloneOperations.Handler;
using Xunit;
using Xunit.Abstractions;

public class StandaloneOperationsTests : TestBase
{
    public StandaloneOperationsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task RunAsync_StandaloneOperations_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            DevServerOptions = new()
            {
                DownloadVersion = "v1.7.4-standalone-nexus-operations",
            },
        });

        var taskQueue = $"tq-{Guid.NewGuid()}";
        await env.CreateNexusEndpointAsync(NexusEndpoints.HelloService, taskQueue);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions(taskQueue).
                AddNexusService(new HelloService()).
                AddWorkflow<HelloHandlerWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var nexusClient = env.Client.CreateNexusClient<IHelloService>(NexusEndpoints.HelloService);

            // Sync (Echo) operation.
            var echoResult = await nexusClient.ExecuteNexusOperationAsync(
                svc => svc.Echo(new("hello-nexus")),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
                });
            Assert.Equal("hello-nexus", echoResult.Message);

            // Async (workflow-backed Hello) operation.
            var helloResult = await nexusClient.ExecuteNexusOperationAsync(
                svc => svc.SayHello(new("Temporal", IHelloService.HelloLanguage.En)),
                new($"op-{Guid.NewGuid()}")
                {
                    ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
                });
            Assert.Equal("Hello Temporal 👋", helloResult.Message);

            // List operations.
            var listCount = 0;
            await foreach (var op in env.Client.ListNexusOperationsAsync(
                $"Endpoint = '{NexusEndpoints.HelloService}'"))
            {
                Assert.NotEmpty(op.OperationId);
                Assert.Equal(NexusEndpoints.HelloService, op.Endpoint);
                listCount++;
            }
            Assert.True(listCount > 0);

            // Count operations.
            var countResp = await env.Client.CountNexusOperationsAsync(
                $"Endpoint = '{NexusEndpoints.HelloService}'");
            Assert.True(countResp.Count > 0);
        });
    }
}
