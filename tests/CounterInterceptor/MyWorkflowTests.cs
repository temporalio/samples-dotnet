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

        // add the interceptor to the client
        var clientOptions = (TemporalClientOptions)env.Client.Options.Clone();
        clientOptions.Interceptors = new[]
        {
            new SimpleClientCallsInterceptor(),
        };

        var client = new TemporalClient(env.Client.Connection, clientOptions);

        var taskQueue = Guid.NewGuid().ToString();

        var workerOptions = new TemporalWorkerOptions(taskQueue).
                AddAllActivities(new MyActivities()).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

        workerOptions.Interceptors = new[] { new SimpleCounterWorkerInterceptor() };

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

            // send exit signal to workflow
            await handle.SignalAsync(wf => wf.ExitAsync());

            // Wait for the workflow to complete
            var result = await handle.GetResultAsync();

            // validate that the worker counters have the correct numbers for the parent
            Assert.Equal(1U, WorkerCounter.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(1U, WorkerCounter.NumOfChildWorkflowExecutions(parentWorkflowId));
            Assert.Equal(0U, WorkerCounter.NumOfActivityExecutions(parentWorkflowId));
            Assert.Equal(2U, WorkerCounter.NumOfSignals(parentWorkflowId));
            Assert.Equal(2U, WorkerCounter.NumOfQueries(parentWorkflowId));

            // validate the worker counters have the correct numbers for the child
            Assert.Equal(1U, WorkerCounter.NumOfWorkflowExecutions(childWorkflowId));
            Assert.Equal(0U, WorkerCounter.NumOfChildWorkflowExecutions(childWorkflowId));
            Assert.Equal(2U, WorkerCounter.NumOfActivityExecutions(childWorkflowId));
            Assert.Equal(0U, WorkerCounter.NumOfSignals(childWorkflowId));
            Assert.Equal(0U, WorkerCounter.NumOfQueries(childWorkflowId));

            // validate the client counters have correct numbers
            Assert.Equal(1U, ClientCounter.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(2U, ClientCounter.NumOfSignals(parentWorkflowId));
            Assert.Equal(2U, ClientCounter.NumOfQueries(parentWorkflowId));
        });
    }
}