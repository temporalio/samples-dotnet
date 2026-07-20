using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.Batch.Iterator;

/// <summary>
/// Fake workflow that implements processing of a single record.
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
        // Simulate some processing.
        await Workflow.DelayAsync(TimeSpan.FromSeconds(Workflow.Random.Next(30)));
        Workflow.Logger.LogInformation("Processed {Record}", record);
    }
}
