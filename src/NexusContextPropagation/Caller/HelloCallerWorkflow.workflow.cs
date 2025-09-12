namespace TemporalioSamples.NexusContextPropagation.Caller;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using TemporalioSamples.ContextPropagation;

[Workflow]
public class HelloCallerWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name, IHelloService.HelloLanguage language)
    {
        Workflow.Logger.LogInformation("Caller workflow called by user {UserId}", MyContext.UserId);
        var output = await Workflow.CreateNexusClient<IHelloService>(IHelloService.EndpointName).
            ExecuteNexusOperationAsync(svc => svc.SayHello(new(name, language)));
        return output.Message;
    }
}