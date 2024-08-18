using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow4PatchComplete
{
    private string? result;

    [WorkflowRun]
    public async Task RunAsync()
    {
        result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result() => result;
}