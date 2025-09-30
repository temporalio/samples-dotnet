namespace TemporalioSamples.NexusMultiArg.Handler;

using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class HelloHandlerWorkflow
{
    [WorkflowRun]
    public async Task<IHelloService.HelloOutput> RunAsync(
        IHelloService.HelloLanguage language, string name) =>
        language switch
        {
            IHelloService.HelloLanguage.En => new($"Hello {name} 👋"),
            IHelloService.HelloLanguage.Fr => new($"Bonjour {name} 👋"),
            IHelloService.HelloLanguage.De => new($"Hallo {name} 👋"),
            IHelloService.HelloLanguage.Es => new($"¡Hola! {name} 👋"),
            IHelloService.HelloLanguage.Tr => new($"Merhaba {name} 👋"),
            _ => throw new ApplicationFailureException(
                $"Unsupported language: {language}", errorType: "UNSUPPORTED_LANGUAGE"),
        };
}