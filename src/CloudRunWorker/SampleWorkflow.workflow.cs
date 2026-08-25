namespace TemporalioSamples.CloudRunWorker;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

// Worker versioning is turned on by TemporalWorkerOptions.ApplyGoogleCloudRunDefaults, which sets the
// deployment's default versioning behavior to Pinned. A workflow can still override that with
// [Workflow(VersioningBehavior = ...)]; this sample relies on the pinned default from the helper.
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
