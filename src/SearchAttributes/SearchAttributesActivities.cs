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
    public async Task<ExecutionInfo> ListExecutionsAsync(string query)
    {
        var context = ActivityExecutionContext.Current;
        context.Logger.LogInformation("Listing executions. Query: {Query}", query);

        // The visibility store is eventually consistent, so poll until the calling execution has
        // been indexed and appears in the results. If it never appears, the activity fails with a
        // start-to-close timeout.
        while (true)
        {
            await foreach (var execution in client.ListWorkflowsAsync(query))
            {
                if (execution.RunId == context.Info.WorkflowRunId)
                {
                    return new(execution.Id, execution.RunId);
                }
            }

            context.Heartbeat();
            await Task.Delay(TimeSpan.FromMilliseconds(300), context.CancellationToken);
        }
    }
}
