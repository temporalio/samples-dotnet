using Temporalio.Workflows;

namespace RoutedVersioning;

public class MyWorkflowV2(StartMyWorkflowRequest args) : IMyWorkflow
{
    private readonly MyWorkflowState state = new() { Args = args };

    public async Task RunAsync(StartMyWorkflowRequest args)
    {
        state.Result = await Workflow.ExecuteActivityAsync(() => Activities.GenericActivity($"I am {args.Value} @ V2"), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        // some new behavior
        await Workflow.DelayAsync(2000);
    }

    public Task CallMeMaybeAsync()
    {
        return Task.CompletedTask;
    }

    public string GetResult()
    {
        return state.Result ?? "incomplete V2";
    }
}