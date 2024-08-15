using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.CounterInterceptor;
using Xunit;
using Xunit.Abstractions;

namespace TemporalioSamples.Tests.CounterInterceptor;

public class MyWorkflowTests : WorkflowEnvironmentTestBase
{
    public MyWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task RunAsync_CounterInterceptor()
    {
        var counterInterceptor = new MyCounterInterceptor();

        // Add the interceptor to the client
        var clientOptions = (TemporalClientOptions)Client.Options.Clone();
        clientOptions.Interceptors = new[]
        {
            counterInterceptor,
        };

        var client = new TemporalClient(Client.Connection, clientOptions);

        var taskQueue = Guid.NewGuid().ToString();

        var workerOptions = new TemporalWorkerOptions(taskQueue).
                AddAllActivities(new MyActivities()).
                AddWorkflow<MyWorkflow>().
                AddWorkflow<MyChildWorkflow>();

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
            Assert.Equal(1U, counterInterceptor.Counts[parentWorkflowId].WorkflowExecutions);
            Assert.Equal(1U, counterInterceptor.Counts[parentWorkflowId].WorkflowChildExecutions);
            Assert.Equal(0U, counterInterceptor.Counts[parentWorkflowId].WorkflowActivityExecutions);
            Assert.Equal(2U, counterInterceptor.Counts[parentWorkflowId].WorkflowSignals);
            Assert.Equal(2U, counterInterceptor.Counts[parentWorkflowId].WorkflowQueries);

            // Validate the worker counters have the correct numbers for the child
            Assert.Equal(1U, counterInterceptor.Counts[childWorkflowId].WorkflowExecutions);
            Assert.Equal(0U, counterInterceptor.Counts[childWorkflowId].WorkflowChildExecutions);
            Assert.Equal(2U, counterInterceptor.Counts[childWorkflowId].WorkflowActivityExecutions);
            Assert.Equal(0U, counterInterceptor.Counts[childWorkflowId].WorkflowSignals);
            Assert.Equal(0U, counterInterceptor.Counts[childWorkflowId].WorkflowQueries);

            // Validate the client counters have correct numbers
            Assert.Equal(1U, counterInterceptor.Counts[parentWorkflowId].ClientExecutions);
            Assert.Equal(2U, counterInterceptor.Counts[parentWorkflowId].ClientSignals);
            Assert.Equal(2U, counterInterceptor.Counts[parentWorkflowId].ClientQueries);
        });
    }
}