namespace TemporalioSamples.Tests.NexusContextPropagation;

using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;
using TemporalioSamples.NexusContextPropagation;
using TemporalioSamples.NexusContextPropagation.Caller;
using TemporalioSamples.NexusContextPropagation.Handler;
using Xunit;
using Xunit.Abstractions;

public class HelloCallerWorkflowTests : WorkflowEnvironmentTestBase
{
    public HelloCallerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_ContextPropagation_ReachesHandlerWorkflow()
    {
        // Create endpoint
        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(IHelloService.EndpointName, handlerTaskQueue);

        // Setup client with interceptors
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.Interceptors =
        [
            new ContextPropagationInterceptor<string?>(
                MyContext.UserIdLocal,
                DataConverter.Default.PayloadConverter),
            new NexusContextPropagationInterceptor(MyContext.UserIdLocal)
        ];
        var client = new TemporalClient(Client.Connection, clientOptions);

        // Run handler worker
        using var handlerWorker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(handlerTaskQueue).
                AddNexusService(new HelloService()).
                AddWorkflow<HelloHandlerWorkflow>());
        await handlerWorker.ExecuteAsync(async () =>
        {
            // Run caller worker
            using var callerWorker = new TemporalWorker(
                client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<HelloCallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                // Set context value, run workflow, confirm result
                MyContext.UserId = "test-user";
                var result = await client.ExecuteWorkflowAsync(
                    (HelloCallerWorkflow wf) => wf.RunAsync("some-name", IHelloService.HelloLanguage.Fr),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));
                Assert.Equal("Bonjour some-name 👋 (user id: test-user)", result);
            });
        });
    }
}