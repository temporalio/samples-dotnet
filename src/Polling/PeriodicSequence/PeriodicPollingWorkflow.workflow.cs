namespace TemporalioSamples.Polling.PeriodicSequence;

using Temporalio.Workflows;

[Workflow]
public class PeriodicPollingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        return await Workflow.ExecuteChildWorkflowAsync(
            (PeriodicPollingChildWorkflow wf) => wf.RunAsync(),
            new()
            {
                Id = "periodic-sequence-polling-sample-child-workflow-id",
            });
    }
}