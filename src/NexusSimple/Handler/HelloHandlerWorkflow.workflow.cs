namespace TemporalioSamples.NexusSimple.Handler;

using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class HelloHandlerWorkflow
{
    [WorkflowRun]
    public async Task<IHelloService.HelloOutput> RunAsync(IHelloService.HelloInput input)
    {
        // This durable timer intentionally exceeds the 10-second Nexus request
        // deadline in the runnable sample. The workflow-backed operation can continue after its
        // start request returns an operation token.
        await Workflow.DelayAsync(TimeSpan.FromSeconds(15));

        return input.Language switch
        {
            IHelloService.HelloLanguage.En => new($"Hello {input.Name} 👋"),
            IHelloService.HelloLanguage.Fr => new($"Bonjour {input.Name} 👋"),
            IHelloService.HelloLanguage.De => new($"Hallo {input.Name} 👋"),
            IHelloService.HelloLanguage.Es => new($"¡Hola! {input.Name} 👋"),
            IHelloService.HelloLanguage.Tr => new($"Merhaba {input.Name} 👋"),
            _ => throw new ApplicationFailureException(
                $"Unsupported language: {input.Language}", errorType: "UNSUPPORTED_LANGUAGE"),
        };
    }
}
