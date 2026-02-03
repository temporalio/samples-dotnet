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
        Workflow.Logger.LogInformation("Workflow called by user {UserId}", MyContext.UserId);

        // Wait for signal then call Nexus service and run activity
        await Workflow.WaitConditionAsync(() => complete);

        // Call Nexus service to demonstrate context propagation through Nexus
        var nexusClient = Workflow.CreateNexusClient<INexusGreetingService>(
            INexusGreetingService.EndpointName);
        var nexusResult = await nexusClient.ExecuteNexusOperationAsync(
            svc => svc.SayGreeting(new(name)));
        Workflow.Logger.LogInformation("Nexus result: {Result}", nexusResult.Message);

        return await Workflow.ExecuteActivityAsync(
            (SayHelloActivities act) => act.SayHello(name),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    [WorkflowSignal]
    public async Task SignalCompleteAsync()
    {
        Workflow.Logger.LogInformation("Signal called by user {UserId}", MyContext.UserId);
        complete = true;
    }

    [WorkflowQuery]
    public bool IsComplete()
    {
        Workflow.Logger.LogInformation("Query called by user {UserId}", MyContext.UserId);
        return complete;
    }
}