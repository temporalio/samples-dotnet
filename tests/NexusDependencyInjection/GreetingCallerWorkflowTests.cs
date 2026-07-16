namespace TemporalioSamples.Tests.NexusDependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Temporalio.Worker;
using TemporalioSamples.NexusDependencyInjection;
using TemporalioSamples.NexusDependencyInjection.Caller;
using TemporalioSamples.NexusDependencyInjection.Handler;
using Xunit;
using Xunit.Abstractions;

public class GreetingCallerWorkflowTests : WorkflowEnvironmentTestBase
{
    public GreetingCallerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_GreetingCallerWorkflow_UsesInjectedDependency()
    {
        // Create the Nexus endpoint that routes to the handler worker's task queue.
        var handlerTaskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(IGreetingService.EndpointName, handlerTaskQueue);

        // Run the handler worker as a generic host exactly as Program.cs does, so the
        // GreetingServiceHandler is resolved via AddScopedNexusService with its IGreetingClient
        // dependency injected by the container.
        using var handlerHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.
                AddScoped<IGreetingClient, GreetingClient>().
                AddHostedTemporalWorker(handlerTaskQueue).
                ConfigureOptions(options => options.ClientOptions = new(Client.Connection.Options.TargetHost!)
                {
                    Namespace = Client.Options.Namespace,
                }).
                AddScopedNexusService<GreetingServiceHandler>())
            .Build();
        await handlerHost.StartAsync();
        try
        {
            // Run the caller worker and execute the workflow that invokes the Nexus operation.
            using var callerWorker = new TemporalWorker(
                Client,
                new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                    AddWorkflow<GreetingCallerWorkflow>());
            await callerWorker.ExecuteAsync(async () =>
            {
                var result = await Client.ExecuteWorkflowAsync(
                    (GreetingCallerWorkflow wf) => wf.RunAsync("Temporal"),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: callerWorker.Options.TaskQueue!));
                Assert.Equal("Hello, Temporal 👋", result);
            });
        }
        finally
        {
            await handlerHost.StopAsync();
        }
    }
}
