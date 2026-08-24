namespace TemporalioSamples.WorkflowStreams;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class HubWorkflow
{
    private bool closed;

    [WorkflowInit]
    public HubWorkflow(HubInput input) => _ = new WorkflowStream(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(HubInput input)
    {
        await Workflow.WaitConditionAsync(() => closed);
        await Workflow.DelayAsync(WorkflowStreamsConstants.DrainDelay);
        return $"hub {input.HubId} closed";
    }

    [WorkflowSignal]
    public async Task CloseAsync() => closed = true;
}
