namespace TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class LlmWorkflow
{
    // Constructing the stream registers its publish Signal, poll Update, and offset Query
    // handlers before the Activity runs. Those handlers keep the stream reachable throughout
    // this Workflow even though only the Activity publishes to it.
#pragma warning disable IDE0052 // The stream is used through its registered handlers.
    private readonly WorkflowStream stream;
#pragma warning restore IDE0052

    [WorkflowInit]
    public LlmWorkflow(LlmInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(LlmInput input)
    {
        var result = await Workflow.ExecuteActivityAsync(
            () => LlmActivities.StreamCompletionAsync(input),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(2), });
        // Poll Updates cannot read the stream after this Workflow closes. This delay is a
        // stopgap that gives active subscribers time to fetch the final events; it does not
        // guarantee delivery to slow subscribers. Use subscriber acknowledgements when needed.
        await Workflow.DelayAsync(Constants.DrainDelay);
        return result;
    }
}
