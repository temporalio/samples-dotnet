using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;
using Xunit;

namespace TemporalioSamples.Tests.ContextPropagation;

public class SayHelloWorkflowTests
{
    [Fact]
    public async Task RunAsync_ContextPropagation_ReachesActivity()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();

        // Update the client to use the interceptor
        var clientOptions = (TemporalClientOptions)env.Client.Options.Clone();
        clientOptions.Interceptors = new[]
        {
            new ContextPropagationInterceptor<string>(
                MyContext.UserId,
                DataConverter.Default.PayloadConverter),
        };
        var client = new TemporalClient(env.Client.Connection, clientOptions);

        // Mock out the activity to assert the context value
        [Activity]
        static string SayHello(string name)
        {
            try
            {
                Assert.Equal("test-user", MyContext.UserId.Value);
            }
            catch (Exception e)
            {
                throw new ApplicationFailureException("Assertion fail", e, nonRetryable: true);
            }
            return $"Mock for {name}";
        }

        // Run worker
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(SayHello).
                AddWorkflow<SayHelloWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Set context value, start workflow, set to something else
            MyContext.UserId.Value = "test-user";
            var handle = await client.StartWorkflowAsync(
                (SayHelloWorkflow wf) => wf.RunAsync("some-name"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            MyContext.UserId.Value = "some-other-value";

            // Send signal, check result
            await handle.SignalAsync(wf => wf.SignalCompleteAsync());
            Assert.Equal("Mock for some-name", await handle.GetResultAsync());
        });
    }
}
