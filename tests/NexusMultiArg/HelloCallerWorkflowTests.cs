namespace TemporalioSamples.Tests.NexusMultiArg;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusMultiArg;
using TemporalioSamples.NexusMultiArg.Caller;
using TemporalioSamples.NexusMultiArg.Handler;
using Xunit;
using Xunit.Abstractions;

public class HelloCallerWorkflowTests : WorkflowEnvironmentTestBase
{
    public HelloCallerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_HelloCallerWorkflow_Succeeds()
    {
        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(IHelloService.EndpointName, handlerTaskQueue);

        // Run handler worker
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