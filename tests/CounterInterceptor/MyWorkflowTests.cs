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

        var workerOptions = new TemporalWorkerOptions(
            TemporalioSamples.CounterInterceptor.Constants.TaskQueue).
                AddAllActivities(new MyActivities()).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

        workerOptions.Interceptors = new[] { new SimpleCounterWorkerInterceptor() };

        var parentWorkflowId = "ParentWorkflowId";
        var childWorkflowId = TemporalioSamples.CounterInterceptor.Constants.ChildWorkflowId;

        using var worker = new TemporalWorker(client, workerOptions);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (MyWorkflow wf) => wf.ExecAsync(),
                new(
                    id: parentWorkflowId,
                    taskQueue: TemporalioSamples.CounterInterceptor.Constants.TaskQueue));

            await handle.SignalAsync(wf => wf.SignalNameAndTitleAsync("John", "Customer"));

            var name = await handle.QueryAsync(wf => wf.QueryName());
            var title = await handle.QueryAsync(wf => wf.QueryTitle());

            // send exit signal to workflow
            await handle.SignalAsync(wf => wf.ExitAsync());

            // Wait for the workflow to complete
            var result = await handle.GetResultAsync();

            // validate that the worker counters have the correct numbers for the parent
            Assert.Equal(1, WorkerCounter.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(1, WorkerCounter.NumOfChildWorkflowExecutions(parentWorkflowId));
            Assert.Equal(0, WorkerCounter.NumOfActivityExecutions(parentWorkflowId));
            Assert.Equal(2, WorkerCounter.NumOfSignals(parentWorkflowId));
            Assert.Equal(2, WorkerCounter.NumOfQueries(parentWorkflowId));

            // validate the worker counters have the correct numbers for the child
            Assert.Equal(1, WorkerCounter.NumOfWorkflowExecutions(childWorkflowId));
            Assert.Equal(0, WorkerCounter.NumOfChildWorkflowExecutions(childWorkflowId));
            Assert.Equal(2, WorkerCounter.NumOfActivityExecutions(childWorkflowId));
            Assert.Equal(0, WorkerCounter.NumOfSignals(childWorkflowId));
            Assert.Equal(0, WorkerCounter.NumOfQueries(childWorkflowId));

            // validate the client counters have correct numbers
            Assert.Equal(1, ClientCounter.NumOfWorkflowExecutions(parentWorkflowId));
            Assert.Equal(2, ClientCounter.NumOfSignals(parentWorkflowId));
            Assert.Equal(2, ClientCounter.NumOfQueries(parentWorkflowId));
        });
    }
}