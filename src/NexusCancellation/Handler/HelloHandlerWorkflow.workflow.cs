namespace TemporalioSamples.NexusCancellation.Handler;

using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class HelloHandlerWorkflow
{
    [WorkflowRun]
    public async Task<IHelloService.HelloOutput> RunAsync(IHelloService.HelloInput input)
    {
        Workflow.Logger.LogInformation(
            "HelloHandlerWorkflow started for {Name} in {Language}",
            input.Name,
            input.Language);

        // Sleep for a random duration to simulate some work
        var duration = TimeSpan.FromSeconds(Workflow.Random.Next(5));

        try
        {
            await Workflow.DelayAsync(duration);
        }
        catch (Exception ex) when (TemporalException.IsCanceledException(ex))
        {
            // Simulate cleanup work after cancellation is requested.
            // Use CancellationToken.None to create a "disconnected" context.
            var cleanupDuration = TimeSpan.FromSeconds(Workflow.Random.Next(5));
            await Workflow.DelayAsync(cleanupDuration, CancellationToken.None);

            Workflow.Logger.LogInformation(
                "HelloHandlerWorkflow for {Name} in {Language} was cancelled after {Duration} of work, performed {CleanupDuration} of cleanup",
                input.Name,
                input.Language,
                duration,
                cleanupDuration);
            throw; // Re-throw the cancellation after cleanup
        }

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
