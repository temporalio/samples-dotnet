namespace TemporalioSamples.NexusSerializationContext;

using Temporalio.Workflows;

[Workflow]
public class EchoWorkflow
{
    [WorkflowRun]
    public Task<string> RunAsync(string message) => Task.FromResult(message);
}
