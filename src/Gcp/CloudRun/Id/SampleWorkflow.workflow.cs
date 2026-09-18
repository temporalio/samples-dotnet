namespace TemporalioSamples.Gcp.CloudRun.Id;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class SampleWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation("SampleWorkflow started: {Name}", name);
        var result = await Workflow.ExecuteActivityAsync(
            () => Activities.SayHello(name),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        Workflow.Logger.LogInformation("SampleWorkflow completed: {Result}", result);
        return result;
    }
}
