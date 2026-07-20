using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Fake workflow that implements processing of a single record. Must report completion to a
/// parent through <see cref="SlidingWindowBatchWorkflow.ReportCompletionAsync"/>.
/// </summary>
[Workflow]
public class RecordProcessorWorkflow
{
    /// <summary>
    /// Processes a single record.
    /// </summary>
    /// <param name="record">Record to process.</param>
    [WorkflowRun]
    public async Task RunAsync(SingleRecord record)
    {
        // Application-specific record processing logic goes here.
        await Workflow.DelayAsync(TimeSpan.FromSeconds(Workflow.Random.Next(10)));
        Workflow.Logger.LogInformation("Processed {Record}", record);

        // This workflow is always expected to have a parent. But for testing it might be useful
        // to skip the notification when run standalone.
        var parentId = Workflow.Info.Parent?.WorkflowId;
        if (parentId is not null)
        {
            var parent = Workflow.GetExternalWorkflowHandle<SlidingWindowBatchWorkflow>(parentId);

            // Notify the parent about record processing completion.
            await parent.SignalAsync(wf => wf.ReportCompletionAsync(record.Id));
        }
    }
}
