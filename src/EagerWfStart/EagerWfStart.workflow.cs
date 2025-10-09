namespace TemporalioSamples.EagerWfStart;

using Temporalio.Workflows;

[Workflow]
public class EagerWfStartWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteLocalActivityAsync(
            (Activities act) => act.Greeting(name),
            new() { ScheduleToCloseTimeout = TimeSpan.FromSeconds(5) });
    }
}