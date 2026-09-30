namespace TemporalioSamples.WorkflowStreams.ExternalPublisher;

using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Workflows;

[Workflow]
public class HubWorkflow
{
    private readonly WorkflowStream stream;
    private bool closed;
    private bool continueAsNewRequested;

    [WorkflowInit]
    public HubWorkflow(HubInput input) => stream = new(input.StreamState);

    [WorkflowRun]
    public async Task<string> RunAsync(HubInput input)
    {
        await Workflow.WaitConditionAsync(
            () => closed || continueAsNewRequested || Workflow.ContinueAsNewSuggested);
        if (!closed)
        {
            var streamState = await stream.CaptureStateForContinueAsNewAsync();
            throw Workflow.CreateContinueAsNewException(
                (HubWorkflow wf) => wf.RunAsync(new(input.HubId, streamState)));
        }

        await Workflow.DelayAsync(Constants.DrainDelay);
        return $"hub {input.HubId} closed";
    }

    [WorkflowSignal]
    public async Task ContinueAsNewAsync() => continueAsNewRequested = true;

    [WorkflowSignal]
    public async Task CloseAsync() => closed = true;
}
