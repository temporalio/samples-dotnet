using Temporalio.Workflows;
using TemporalioSamples.Polling.Common;

namespace TemporalioSamples.Polling.PeriodicSequence;

[Workflow]
public class PeriodicPollingWorkflow : IPollingWorkflow
{
    // Set some periodic poll interval, for sample we set 5 seconds
    private int pollingIntervalInSeconds = 5;

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        return await Workflow.ExecuteChildWorkflowAsync(
            (PeriodicPollingChildWorkflow wf) => wf.RunAsync(new(pollingIntervalInSeconds)),
            new()
            {
                ID = "periodic-sequence-polling-sample-child-workflow-id",
            });
    }
}