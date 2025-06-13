namespace TemporalioSamples.ContextPropagation;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class SayHelloWorkflow
{
    private bool complete;

    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation(
            "Workflow called by user {User}",
            IdentityContext.User.Value);

        // Wait for signal then run activity
        await Workflow.WaitConditionAsync(() => complete);
        return await Workflow.ExecuteActivityAsync(
            (SayHelloActivities act) => act.SayHello(name),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowSignal]
    public async Task SignalCompleteAsync()
    {
        Workflow.Logger.LogInformation(
            "Signal called by user {UserId}",
            IdentityContext.User.Value);
        complete = true;
    }

    [WorkflowQuery]
    public bool IsComplete()
    {
        Workflow.Logger.LogInformation(
            "Query called by user {UserId}",
            IdentityContext.User.Value);
        return complete;
    }
}