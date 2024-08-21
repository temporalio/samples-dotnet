using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow3PatchDeprecated
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.DeprecatePatch("my-patch");
        Result = await Workflow.ExecuteActivityAsync(() => Activities.PostPatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result { get; private set; }
}