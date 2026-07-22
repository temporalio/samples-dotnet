using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;

namespace TemporalioSamples.SearchAttributes;

public record ExecutionInfo(string WorkflowId, string RunId);

public class SearchAttributesActivities
{
    private readonly ITemporalClient client;

    public SearchAttributesActivities(ITemporalClient client) => this.client = client;

    [Activity]
    public async Task<ExecutionInfo> WaitForFirstMatchingExecutionAsync(string query)
    {
        var context = ActivityExecutionContext.Current;

        // Scope the caller's query to the calling execution so we don't list every workflow in
        // the namespace.
        var scopedQuery = $"({query}) AND RunId = '{context.Info.WorkflowRunId}'";
        context.Logger.LogInformation("Waiting for first matching execution. Query: {Query}", scopedQuery);

        // The visibility store is eventually consistent, so poll until the calling execution has
        // been indexed and appears in the results. If it never appears, the activity fails with a
        // start-to-close timeout.
        while (true)
        {
            await foreach (var execution in client.ListWorkflowsAsync(scopedQuery))
            {
                return new(execution.Id, execution.RunId);
            }

            context.Heartbeat();
            await Task.Delay(TimeSpan.FromMilliseconds(300), context.CancellationToken);
        }
    }
}
