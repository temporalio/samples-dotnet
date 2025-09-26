namespace TemporalioSamples.NexusContextPropagation.Handler;

using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using TemporalioSamples.ContextPropagation;

[Workflow]
public class HelloHandlerWorkflow
{
    [WorkflowRun]
    public async Task<IHelloService.HelloOutput> RunAsync(IHelloService.HelloInput input)
    {
        Workflow.Logger.LogInformation("Handler workflow called by user {UserId}", MyContext.UserId);
        var message = input.Language switch
        {
            IHelloService.HelloLanguage.En => $"Hello {input.Name} 👋",
            IHelloService.HelloLanguage.Fr => $"Bonjour {input.Name} 👋",
            IHelloService.HelloLanguage.De => $"Hallo {input.Name} 👋",
            IHelloService.HelloLanguage.Es => $"¡Hola! {input.Name} 👋",
            IHelloService.HelloLanguage.Tr => $"Merhaba {input.Name} 👋",
            _ => throw new ApplicationFailureException(
                $"Unsupported language: {input.Language}", errorType: "UNSUPPORTED_LANGUAGE"),
        };
        return new($"{message} (user id: {MyContext.UserId})");
    }
}