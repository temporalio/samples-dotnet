namespace TemporalioSamples.LambdaWorker;

using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

[Workflow(VersioningBehavior = VersioningBehavior.Pinned)]
public class SampleWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation("SampleWorkflow started with name: {Name}", name);
        var result = await Workflow.ExecuteActivityAsync(
            () => Activities.HelloActivity(name),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        Workflow.Logger.LogInformation("SampleWorkflow completed with result: {Result}", result);
        return result;
    }
}
