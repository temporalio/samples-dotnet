using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow3PatchDeprecated
{
    private string? result;

    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.DeprecatePatch("my-patch");
        result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result() => result;
}