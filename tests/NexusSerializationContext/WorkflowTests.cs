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
    public async Task EightOperations_PreserveNativeAsyncContextAsync()
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
            var handle = await caller.StartWorkflowAsync(
                (CallerWorkflow workflow) => workflow.RunAsync("Hello from Nexus"), new($"caller-{Guid.NewGuid()}", "caller"));
            var results = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(SampleConfig.Endpoints, results.Select(r => r.Endpoint));
            Assert.All(results, result =>
            {
                Assert.Equal("Hello from Nexus", result.Sync);
                Assert.Equal("Hello from Nexus", result.Async);
            });
            var callerHistory = await caller.WorkflowService.GetWorkflowExecutionHistoryAsync(new()
            {
                Namespace = caller.Options.Namespace,
                Execution = new() { WorkflowId = handle.Id },
            });
            Assert.Equal(8, callerHistory.History.Events.Count(e => e.NexusOperationScheduledEventAttributes != null));
            Assert.Equal(8, callerHistory.History.Events.
                TakeWhile(e => e.NexusOperationCompletedEventAttributes == null).
                Count(e => e.NexusOperationScheduledEventAttributes != null));
            var asyncRequests = callerHistory.History.Events.
                Select(e => e.NexusOperationScheduledEventAttributes).
                Where(e => e?.Service == "AsyncEchoService").
                ToDictionary(e => e.Endpoint, e => e.RequestId);
            var input = callerHistory.History.Events[0].WorkflowExecutionStartedEventAttributes.Input.Payloads_[0];
            Assert.Equal("caller-workflow", input.Metadata["encryption-key-id"].ToStringUtf8());
            var final = callerHistory.History.Events.Last().WorkflowExecutionCompletedEventAttributes.Result.Payloads_[0];
            Assert.Equal("caller-workflow", final.Metadata["encryption-key-id"].ToStringUtf8());
            Assert.False(final.Metadata.ContainsKey("nexus-endpoint-name"));

            // Visibility is eventually consistent; bound the wait for the four backing executions.
            var deadline = DateTime.UtcNow.AddSeconds(20);
            WorkflowListPage executions;
            do
            {
                executions = await caller.ListWorkflowsPaginatedAsync("WorkflowType = 'EchoWorkflow'", null);
                if (executions.Workflows.Count == 4)
                {
                    break;
                }
                await Task.Delay(200);
            }
            while (DateTime.UtcNow < deadline);
            Assert.Equal(4, executions.Workflows.Count);
            var seen = new HashSet<string>();
            foreach (var execution in executions.Workflows)
            {
                Assert.StartsWith("nexus-echo-", execution.Id);
                var history = await caller.WorkflowService.GetWorkflowExecutionHistoryAsync(new()
                {
                    Namespace = caller.Options.Namespace,
                    Execution = new() { WorkflowId = execution.Id },
                });
                var started = history.History.Events[0].WorkflowExecutionStartedEventAttributes;
                var context = started.PropagatedNexusSerializationContext;
                Assert.NotNull(context);
                Assert.Equal("AsyncEchoService", context.Service);
                Assert.Equal("echoAsync", context.Operation);
                Assert.True(seen.Add(context.Endpoint));
                Assert.Contains(context.Endpoint, SampleConfig.Endpoints);
                Assert.Equal($"nexus-echo-{asyncRequests[context.Endpoint]}", execution.Id);
                var workflowInput = started.Input.Payloads_[0];
                Assert.Equal(
                    context.Endpoint.Contains("compressed") ? "compressed-encrypted-1" : "encrypted-1",
                    workflowInput.Metadata["encryption-key-id"].ToStringUtf8());
                Assert.False(workflowInput.Metadata.ContainsKey("nexus-endpoint-name"));
                var result = history.History.Events.Last().WorkflowExecutionCompletedEventAttributes.Result.Payloads_[0];
                Assert.Equal(context.Endpoint, result.Metadata["nexus-endpoint-name"].ToStringUtf8());
                Assert.Equal(context.Endpoint["nexus-serialization-".Length..], result.Metadata["encryption-key-id"].ToStringUtf8());
            }
        })));
    }
}
