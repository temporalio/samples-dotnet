namespace TemporalioSamples.Tests.Batch.Iterator;

using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.Batch.Iterator;
using Xunit;
using Xunit.Abstractions;

public class IteratorBatchWorkflowTests : TestBase
{
    public IteratorBatchWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_FullDataset_ProcessesAllPagesAndContinuesAsNew()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddAllActivities(typeof(RecordLoaderActivities), null).
                AddWorkflow<IteratorBatchWorkflow>().
                AddWorkflow<RecordProcessorWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (IteratorBatchWorkflow wf) => wf.RunAsync(5, 0),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // The fake RecordLoaderActivities always returns 5 pages of 5 records each.
            var result = await handle.GetResultAsync();
            Assert.Equal(25, result);

            // Confirm the workflow actually exercised continue-as-new to get there.
            var firstRunHistory = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
            Assert.Contains(
                firstRunHistory.Events,
                e => e.WorkflowExecutionContinuedAsNewEventAttributes != null);
        });
    }
}
