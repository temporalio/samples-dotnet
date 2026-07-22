namespace TemporalioSamples.NexusSimple.Caller;

using Temporalio.Workflows;

[Workflow]
public class EchoCallerWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string message)
    {
        var output = await Workflow.CreateNexusWorkflowClient<IHelloService>(NexusEndpoints.HelloService).
            ExecuteNexusOperationAsync(svc => svc.Echo(new(message)));
        return output.Message;
    }
}