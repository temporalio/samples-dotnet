namespace TemporalioSamples.Tests.NexusSerializationContext;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.NexusSerializationContext;
using Xunit;

public class WorkflowTests
{
    [Fact]
    public async Task RunAsync_AllEndpoints_Succeeds()
    {
        // Point this at the serialization-context CLI build described in the sample README.
        await using var env = await WorkflowEnvironment.StartLocalAsync(new()
        {
            DevServerOptions = new()
            {
                ExistingPath = Environment.GetEnvironmentVariable("TEMPORAL_SERIALIZATION_CONTEXT_CLI"),
                DownloadVersion = "v1.9.3-nexus-serialization-context",
            },
        });
        TemporalClient ClientWith(DataConverter converter) => new(
            env.Client.Connection,
            new() { Namespace = env.Client.Options.Namespace, DataConverter = converter });
        var caller = ClientWith(SampleConfig.CallerConverter());
        var compressed = ClientWith(SampleConfig.HandlerConverter(true));
        var encrypted = ClientWith(SampleConfig.HandlerConverter(false));
        foreach (var endpoint in SampleConfig.Endpoints)
        {
            await env.CreateNexusEndpointAsync(endpoint, endpoint.Contains("compressed") ? "compressed" : "encrypted");
        }
        using var compressedWorker = new TemporalWorker(compressed, new TemporalWorkerOptions("compressed").
            AddWorkflow<EchoWorkflow>().AddNexusService(new SyncEchoService()).AddNexusService(new AsyncEchoService()));
        using var encryptedWorker = new TemporalWorker(encrypted, new TemporalWorkerOptions("encrypted").
            AddWorkflow<EchoWorkflow>().AddNexusService(new SyncEchoService()).AddNexusService(new AsyncEchoService()));
        using var callerWorker = new TemporalWorker(caller, new TemporalWorkerOptions("caller").AddWorkflow<CallerWorkflow>());
        await compressedWorker.ExecuteAsync(() => encryptedWorker.ExecuteAsync(() => callerWorker.ExecuteAsync(async () =>
        {
            var results = await caller.ExecuteWorkflowAsync(
                (CallerWorkflow workflow) => workflow.RunAsync("Hello from Nexus"),
                new($"caller-{Guid.NewGuid()}", "caller")).WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(SampleConfig.Endpoints, results.Select(r => r.Endpoint));
            Assert.All(results, result =>
            {
                Assert.Equal("Hello from Nexus", result.Sync);
                Assert.Equal("Hello from Nexus", result.Async);
            });
        })));
    }
}
