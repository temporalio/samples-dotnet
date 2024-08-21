using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow1Initial
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Result = await Workflow.ExecuteActivityAsync(() => Activities.PrePatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result { get; private set; }
}