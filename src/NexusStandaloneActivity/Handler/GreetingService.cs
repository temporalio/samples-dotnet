namespace TemporalioSamples.NexusStandaloneActivity.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(IGreetingService))]
public class GreetingService
{
    [NexusOperationHandler]
    public IOperationHandler<IGreetingService.GreetingInput, IGreetingService.GreetingOutput> Greet() =>
        // TemporalOperationHandler maps a Temporal execution onto a Nexus Operation. Here the execution
        // is a Standalone Activity: StartActivityAsync returns an asynchronous operation result, so the
        // Nexus Operation stays running until the Activity completes, at which point Temporal delivers
        // the Activity's result to the Nexus caller.
        TemporalOperationHandler.FromHandleFactory<
            IGreetingService.GreetingInput, IGreetingService.GreetingOutput>(
            (ctx, client, input) => client.StartActivityAsync(
                () => GreetingActivities.CreateGreetingAsync(input),
                new()
                {
                    Id = $"greeting-{input.Name}",
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                }));
}
