using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.CounterInterceptor;
using Xunit;

namespace TemporalioSamples.Tests.CounterInterceptor;

public class MyWorkflowTests
{
    [Fact]
    public async Task RunAsync_CounterInterceptor()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();

        var clientInterceptor = new SimpleClientCallsInterceptor();

        // Add the interceptor to the client
        var clientOptions = (TemporalClientOptions)env.Client.Options.Clone();
        clientOptions.Interceptors = new[]
        {
            clientInterceptor,
        };

        var client = new TemporalClient(env.Client.Connection, clientOptions);

        var taskQueue = Guid.NewGuid().ToString();

        var workerOptions = new TemporalWorkerOptions(taskQueue).
                AddAllActivities(new MyActivities()).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

        var workerInterceptor = new SimpleCounterWorkerInterceptor();
        workerOptions.Interceptors = new[] { workerInterceptor };

        var parentWorkflowId = "ParentWorkflowId";
        // Be sure that this matches the ID in the Workflow
        var childWorkflowId = "counter-interceptor-child";

        using var worker = new TemporalWorker(client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.RunAsync(),
                new(
                    id: parentWorkflowId,
                    taskQueue: taskQueue));

            await handle.SignalAsync(wf => wf.SignalNameAndTitleAsync("John", "Customer"));

            var name = await handle.QueryAsync(wf => wf.Name);
            var title = await handle.QueryAsync(wf => wf.Title);

            // Send exit signal to workflow
            await handle.SignalAsync(wf => wf.ExitAsync());

            // Wait for the workflow to complete
            var result = await handle.GetResultAsync();

            // Validate that the worker counters have the correct numbers for the parent
            Assert.Equal(1U, workerInterceptor.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(1U, workerInterceptor.NumOfChildWorkflowExecutions(parentWorkflowId));
            Assert.Equal(0U, workerInterceptor.NumOfActivityExecutions(parentWorkflowId));
            Assert.Equal(2U, workerInterceptor.NumOfSignals(parentWorkflowId));
            Assert.Equal(2U, workerInterceptor.NumOfQueries(parentWorkflowId));

            // Validate the worker counters have the correct numbers for the child
            Assert.Equal(1U, workerInterceptor.NumOfWorkflowExecutions(childWorkflowId));
            Assert.Equal(0U, workerInterceptor.NumOfChildWorkflowExecutions(childWorkflowId));
            Assert.Equal(2U, workerInterceptor.NumOfActivityExecutions(childWorkflowId));
            Assert.Equal(0U, workerInterceptor.NumOfSignals(childWorkflowId));
            Assert.Equal(0U, workerInterceptor.NumOfQueries(childWorkflowId));

            // Validate the client counters have correct numbers
            Assert.Equal(1U, clientInterceptor.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(2U, clientInterceptor.NumOfSignals(parentWorkflowId));
            Assert.Equal(2U, clientInterceptor.NumOfQueries(parentWorkflowId));
        });
    }
}