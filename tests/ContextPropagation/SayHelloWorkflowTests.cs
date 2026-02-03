namespace TemporalioSamples.Tests.ContextPropagation;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;
using Xunit;
using Xunit.Abstractions;

public class SayHelloWorkflowTests : WorkflowEnvironmentTestBase
{
    public SayHelloWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_ContextPropagation_ReachesActivity()
    {
        // Create Nexus endpoint for testing context propagation through Nexus
        var taskQueue = $"tq-{Guid.NewGuid()}";
        await Env.TestEnv.CreateNexusEndpointAsync(
            INexusGreetingService.EndpointName, taskQueue);

        // Update the client to use the interceptor
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.Interceptors =
        [
            new ContextPropagationInterceptor<string?>(
                MyContext.UserIdLocal,
                DataConverter.Default.PayloadConverter),
        ];
        var client = new TemporalClient(Client.Connection, clientOptions);

        // Mock out the activity to assert the context value
        [Activity]
        static string SayHello(string name)
        {
            try
            {
                Assert.Equal("test-user", MyContext.UserId);
            }
            catch (Exception e)
            {
                throw new ApplicationFailureException("Assertion fail", e, nonRetryable: true);
            }
            return $"Mock for {name}";
        }

        // Run worker with Nexus service, handler workflow, and activity
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(taskQueue).
                AddActivity(SayHello).
                AddWorkflow<SayHelloWorkflow>().
                AddNexusService(new NexusGreetingService()).
                AddWorkflow<NexusGreetingHandlerWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Set context value, start workflow, set to something else
            MyContext.UserId = "test-user";
            var handle = await client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync("some-name"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            MyContext.UserId = "some-other-value";

            // Send signal, check result
            await handle.SignalAsync(wf => wf.SignalCompleteAsync());
            Assert.Equal("Mock for some-name", await handle.GetResultAsync());

            // Verify context propagated through Nexus to handler workflow
            var history = await handle.FetchHistoryAsync();
            var nexusStartedEvent = history.Events.First(e => e.NexusOperationStartedEventAttributes != null);
            var handlerWorkflowId = nexusStartedEvent.Links.First().WorkflowEvent!.WorkflowId;
            var handlerHandle = client.GetWorkflowHandle<NexusGreetingHandlerWorkflow>(handlerWorkflowId);
            Assert.Equal("test-user", await handlerHandle.QueryAsync(wf => wf.CapturedUserId));
        });
    }
}
