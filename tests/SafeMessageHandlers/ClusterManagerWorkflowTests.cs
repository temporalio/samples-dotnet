namespace TemporalioSamples.Tests.SafeMessageHandlers;

using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.SafeMessageHandlers;
using Xunit;
using Xunit.Abstractions;

public class ClusterManagerWorkflowTests : WorkflowEnvironmentTestBase
{
    public ClusterManagerWorkflowTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartWorkflowAsync_SimpleJobSet_Succeeds(bool testContinueAsNew)
    {
        // Run inside worker
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddAllActivities(new ClusterManagerActivities()).
                AddWorkflow<ClusterManagerWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Start workflow
            var workflowOptions = new WorkflowOptions(
                    id: $"wf-{Guid.NewGuid()}",
                    taskQueue: worker.Options.TaskQueue!);
            workflowOptions.SignalWithStart((ClusterManagerWorkflow wf) => wf.StartClusterAsync());
            var handle = await Client.StartWorkflowAsync(
                (ClusterManagerWorkflow wf) => wf.RunAsync(new() { TestContinueAsNew = testContinueAsNew }),
                workflowOptions);

            // Allocate 2 nodes each to 6 jobs
            var nodeSets = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
                handle.ExecuteUpdateAsync(wf => wf.AllocateNodesToJobAsync(new(2, $"job-{i}")))));
            Assert.Equal(6, nodeSets.Length);
            Assert.All(nodeSets, nodes => Assert.Equal(2, nodes.Count));

            // Confirm that some jobs are assigned
            var state = await handle.QueryAsync(wf => wf.CurrentState);
            Assert.True(state.ClusterStarted);
            Assert.False(state.ClusterShutdown);
            Assert.Equal(2, state.Nodes.Count(kvp => kvp.Value == "job-0"));

            // Delete all the jobs, shutdown the cluster, and confirm result
            await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
                handle.ExecuteUpdateAsync(wf => wf.DeleteJobAsync(new($"job-{i}")))));
            await handle.SignalAsync(wf => wf.ShutdownClusterAsync());
            var result = await handle.GetResultAsync();
            Assert.Equal(12, result.MaxAssignedNodes);
            Assert.Equal(0, result.NumAssignedNodes);

            // Check whether the workflow continued as new
            var firstRunHistory = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
            bool continued = firstRunHistory.Events.Any(
                evt => evt.WorkflowExecutionContinuedAsNewEventAttributes != null);
            Assert.Equal(testContinueAsNew, continued);
        });
    }
}