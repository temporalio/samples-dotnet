namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class LlmWorkflow
{
    [WorkflowInit]
    public LlmWorkflow(LlmInput input) => _ = new WorkflowStream(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(LlmInput input)
    {
        var result = await Workflow.ExecuteActivityAsync(
            () => LlmActivities.StreamCompletionAsync(input),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(2), });
        await Workflow.DelayAsync(WorkflowStreamsConstants.DrainDelay);
        return result;
    }
}
