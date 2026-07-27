namespace TemporalioSamples.NexusSimple.Caller;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class HelloCallerWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name, IHelloService.HelloLanguage language)
    {
        var client = Workflow.CreateNexusWorkflowClient<IHelloService>(NexusEndpoints.HelloService);
        var handle = await client.StartNexusOperationAsync(svc => svc.SayHello(new(name, language)));

        Workflow.Logger.LogInformation(
            "Async SayHello operation started; waiting for the workflow-backed result");
        var output = await handle.GetResultAsync();
        Workflow.Logger.LogInformation("Async SayHello operation completed");

        return output.Message;
    }
}
