using Temporalio;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    public const string TaskQueue = "asp-net-sample";

    public static readonly MyWorkflow Ref = Refs.Create<MyWorkflow>();

    [WorkflowRun]
    public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
}