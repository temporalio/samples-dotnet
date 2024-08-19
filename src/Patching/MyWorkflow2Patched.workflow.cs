using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow2Patched
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        if (Workflow.Patched("my-patch"))
        {
            Result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
        else
        {
            Result = await Workflow.ExecuteActivityAsync(() => Activities.PrePatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }

    [WorkflowQuery]
    public string? Result { get; private set; }
}