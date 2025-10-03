namespace TemporalioSamples.Tests.NexusSimple;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusSimple;
using TemporalioSamples.NexusSimple.Caller;
using TemporalioSamples.NexusSimple.Handler;
using Xunit;
using Xunit.Abstractions;

public class CallerWorkflowTests : WorkflowEnvironmentTestBase
{
    private static Task<string>? lazyHandlerTaskQueue;

    public CallerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    public Task<string> EnsureHandlerTaskQueueAsync() =>
        LazyInitializer.EnsureInitialized(ref lazyHandlerTaskQueue, async () =>
        {
            var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
            await Env.TestEnv.CreateNexusEndpointAsync(IHelloService.EndpointName, handlerTaskQueue);
            return handlerTaskQueue;
        });

    [Fact]
    public async Task RunAsync_EchoCallerWorkflow_Succeeds()
    {
        // Run handler worker
        var handlerTaskQueue = await EnsureHandlerTaskQueueAsync();
        using var handlerWorker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new HelloService()).
                AddWorkflow<HelloHandlerWorkflow>());
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<EchoCallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                // Run workflow, confirm result
                var result = await Client.ExecuteWorkflowAsync(
                    (EchoCallerWorkflow wf) => wf.RunAsync("some-message"),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));
                Assert.Equal("some-message", result);
            });
        });
    }

    [Fact]
    public async Task RunAsync_HelloCallerWorkflow_Succeeds()
    {
        // Run handler worker
        var handlerTaskQueue = await EnsureHandlerTaskQueueAsync();
        using var handlerWorker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new HelloService()).
                AddWorkflow<HelloHandlerWorkflow>());
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<HelloCallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                // Run workflow, confirm result
                var result = await Client.ExecuteWorkflowAsync(
                    (HelloCallerWorkflow wf) => wf.RunAsync("some-name", IHelloService.HelloLanguage.Fr),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));
                Assert.Equal("Bonjour some-name 👋", result);
            });
        });
    }
}