using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow2Patched
{
    private string? result;

    [WorkflowRun]
    public async Task RunAsync()
    {
        if (Workflow.Patched("my-patch"))
        {
            result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
        else
        {
            result = await Workflow.ExecuteActivityAsync(() => Activities.PrePatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }

    [WorkflowQuery]
    public string? Result() => result;
}