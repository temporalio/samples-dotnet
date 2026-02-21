using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.SampleWorkflow;

[Workflow]
public class SimpleWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Logger.LogInformation("Starting workflow...");
        await Workflow.ExecuteActivityAsync((SimpleActivities a) => a.DoSomethingAsync(),  new() { StartToCloseTimeout = TimeSpan.FromMinutes(4) });
        Workflow.Logger.LogInformation("Workflow completed!");
    }
}