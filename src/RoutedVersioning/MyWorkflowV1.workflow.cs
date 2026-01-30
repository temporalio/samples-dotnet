using Temporalio.Workflows;

namespace RoutedVersioning;

public class MyWorkflowV1(StartMyWorkflowRequest args) : IMyWorkflow
{
    private readonly MyWorkflowState state = new() { Args = args };

    public async Task RunAsync(StartMyWorkflowRequest args)
    {
        state.Result = await Workflow.ExecuteActivityAsync(() => Activities.GenericActivity($"I am {args.Value} @ V1"), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    public Task CallMeMaybeAsync()
    {
        return Task.CompletedTask;
    }

    public string GetResult()
    {
        return state.Result ?? "incomplete V1";
    }
}