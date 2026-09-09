namespace TemporalioSamples.Gcp.CloudRun.WorkerId;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

// A minimal greeting workflow that runs one activity. The WorkerIdPlugin only sets the worker
// identity, so this workflow runs exactly as it would on any other worker.
[Workflow]
public class SampleWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        Workflow.Logger.LogInformation("SampleWorkflow started with name: {Name}", name);
        var result = await Workflow.ExecuteActivityAsync(
            () => Activities.SayHello(name),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        Workflow.Logger.LogInformation("SampleWorkflow completed with result: {Result}", result);
        return result;
    }
}
