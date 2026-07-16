namespace TemporalioSamples.NexusDependencyInjection.Caller;

using Temporalio.Workflows;

[Workflow]
public class GreetingCallerWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        var nexusClient = Workflow.CreateNexusWorkflowClient<IGreetingService>(IGreetingService.EndpointName);
        return await nexusClient.ExecuteNexusOperationAsync(svc => svc.SayHello(new(name)));
    }
}
