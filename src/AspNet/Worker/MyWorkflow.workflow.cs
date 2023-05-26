namespace TemporalioSamples.AspNet.Worker;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    public const string TaskQueue = "asp-net-sample";

    [WorkflowRun]
    public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
}