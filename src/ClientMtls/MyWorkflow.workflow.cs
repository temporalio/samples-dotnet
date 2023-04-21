namespace TemporalioSamples.ClientMtls;

using Temporalio.Workflows;

[Workflow]
public class GreetingWorkflow
{
    public static readonly GreetingWorkflow Ref = WorkflowRefs.Create<GreetingWorkflow>();

    [WorkflowRun]
    public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
}