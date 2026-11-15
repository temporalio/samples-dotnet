namespace TemporalioSamples.ExternalStorage;

using Temporalio.Workflows;

// The workflow's own input and result are small. The order list passed from FetchOrders to
// SummarizeOrders is the payload large enough to be offloaded to external storage.
[Workflow]
public class ProcessOrderBatchWorkflow
{
    [WorkflowRun]
    public async Task<BatchSummary> RunAsync(int orderCount)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) };
        var orders = await Workflow.ExecuteActivityAsync(
            () => OrderActivities.FetchOrders(orderCount), options);
        return await Workflow.ExecuteActivityAsync(
            () => OrderActivities.SummarizeOrders(orders), options);
    }
}
