namespace TemporalioSamples.Tests.Batch.SlidingWindow;

using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.Batch.SlidingWindow;
using Xunit;
using Xunit.Abstractions;

public class SlidingWindowBatchWorkflowTests : TestBase
{
    public SlidingWindowBatchWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_ThreePartitions_ProcessesEveryRecordExactlyOnce()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddAllActivities(typeof(RecordLoaderActivities), null).
                AddWorkflow<BatchWorkflow>().
                AddWorkflow<SlidingWindowBatchWorkflow>().
                AddWorkflow<RecordProcessorWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (BatchWorkflow wf) => wf.RunAsync(10, 25, 3),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            // The fake RecordLoaderActivities always has 300 total records, split evenly across
            // the 3 partitions. Each partition's SlidingWindowBatchWorkflow must report exactly
            // its own share back to the parent, not a cumulative or duplicated count.
            var result = await handle.GetResultAsync();
            Assert.Equal(300, result);
        });
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_TestContinueAsNewSet_ContinuesAsNewBeforePageSizeIsReached()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").
                AddAllActivities(typeof(RecordLoaderActivities), null).
                AddWorkflow<SlidingWindowBatchWorkflow>().
                AddWorkflow<RecordProcessorWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            // Only processes a small slice of the dataset (rather than the full 300 records) so
            // that even in the worst case of continuing-as-new after every single child, the run
            // still finishes quickly. PageSize is far larger than that slice, so it would never
            // be reached naturally: only the TestContinueAsNew-driven maxHistoryLength check can
            // cause a continue-as-new here.
            var input = new ProcessBatchInput
            {
                PageSize = 1000,
                SlidingWindowSize = 10,
                Offset = 0,
                MaximumOffset = 30,
                TestContinueAsNew = true,
            };
            var handle = await env.Client.StartWorkflowAsync(
                (SlidingWindowBatchWorkflow wf) => wf.RunAsync(input),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            var result = await handle.GetResultAsync();
            Assert.Equal(30, result);

            // Confirm continue-as-new actually happened, proving the maxHistoryLength branch
            // fired rather than the unreachable pageSize branch.
            var firstRunHistory = await (handle with { RunId = handle.ResultRunId }).FetchHistoryAsync();
            Assert.Contains(
                firstRunHistory.Events,
                e => e.WorkflowExecutionContinuedAsNewEventAttributes != null);
        });
    }
}
