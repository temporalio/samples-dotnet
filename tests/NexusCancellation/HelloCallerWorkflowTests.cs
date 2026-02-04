namespace TemporalioSamples.Tests.NexusCancellation;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.NexusCancellation;
using TemporalioSamples.NexusCancellation.Caller;
using TemporalioSamples.NexusCancellation.Handler;
using Xunit;
using Xunit.Abstractions;

public class HelloCallerWorkflowTests : WorkflowEnvironmentTestBase
{
    private static readonly string[] ExpectedGreetings =
    [
        "Hello Temporal 👋",
        "Bonjour Temporal 👋",
        "Hallo Temporal 👋",
        "¡Hola! Temporal 👋",
        "Merhaba Temporal 👋",
    ];

    public HelloCallerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_ReturnsFirstCompletedGreeting()
    {
        // Create endpoint
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
                // Run workflow, confirm it returns a valid greeting
                var result = await Client.ExecuteWorkflowAsync(
                    (HelloCallerWorkflow wf) => wf.RunAsync("Temporal"),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));

                // Should return one of the valid greetings (whichever completes first)
                Assert.Contains(result, ExpectedGreetings);
            });
        });
    }
}
