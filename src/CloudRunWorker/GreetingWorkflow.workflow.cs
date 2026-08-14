namespace TemporalioSamples.CloudRunWorker;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation("GreetingWorkflow started: {Name}", name);
        var result = await Workflow.ExecuteActivityAsync(
            () => GreetingActivities.SayHello(name),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        Workflow.Logger.LogInformation("GreetingWorkflow completed: {Result}", result);
        return result;
    }
}
