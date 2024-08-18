using Temporalio.Workflows;

namespace TemporalioSamples.Patching;

[Workflow("MyWorkflow")]
public class MyWorkflow1Initial
{
    private string? result;

    [WorkflowRun]
    public async Task RunAsync()
    {
        result = await Workflow.ExecuteActivityAsync(() => Activities.PrePatchActivity(), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowQuery]
    public string? Result()
    {
        return result;
    }
}