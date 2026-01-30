using Temporalio.Workflows;

namespace RoutedVersioning;

// This is the file where all the latest behaviors should be updated.
// This lets you have a nice `git diff` experience while being able to maintain the same WorkflowType name for replay.
public class MyWorkflowLatest(StartMyWorkflowRequest args) : IMyWorkflow
{
    private readonly MyWorkflowState state = new() { Args = args };

    public async Task RunAsync(StartMyWorkflowRequest args)
    {
        state.Result = await Workflow.ExecuteActivityAsync(() => Activities.GenericActivity($"V3 is new hotness @ {args.Value}\n"), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });

        // some very different behavior
        state.Result += await Workflow.ExecuteActivityAsync(() => Activities.GenericActivity("This will appear in the git diff for code review nicely!"), new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    public Task CallMeMaybeAsync()
    {
        return Task.CompletedTask;
    }

    public string GetResult()
    {
        return state.Result ?? "not this time V3";
    }
}