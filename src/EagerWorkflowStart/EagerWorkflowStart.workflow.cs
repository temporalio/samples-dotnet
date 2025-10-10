namespace TemporalioSamples.EagerWorkflowStart;

using Temporalio.Workflows;

[Workflow]
public class EagerWorkflowStartWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteLocalActivityAsync(
            (Activities act) => act.Greeting(name),
            new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) });
    }
}