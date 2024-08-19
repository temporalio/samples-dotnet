using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow4PatchComplete
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result { get; private set; }
}