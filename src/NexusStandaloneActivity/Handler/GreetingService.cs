namespace TemporalioSamples.NexusStandaloneActivity.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;

// Declares a single operation whose backing execution is a Standalone Activity.
[NexusServiceHandler(typeof(IGreetingService))]
public class GreetingService
{
    [TemporalOperation]
#pragma warning disable VSTHRD200 // Name must match IGreetingService.Greet, which can't take the Async suffix
    public Task<TemporalOperationResult<IGreetingService.GreetingOutput>> Greet(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        IGreetingService.GreetingInput input) =>
        // StartActivityAsync returns an asynchronous operation result, so the Nexus Operation
        // stays running until the Activity completes, at which point Temporal delivers the
        // Activity's result to the Nexus caller.
        client.StartActivityAsync(
            () => GreetingActivities.CreateGreetingAsync(input),
            new()
            {
                Id = $"greeting-{input.Name}",
                StartToCloseTimeout = TimeSpan.FromSeconds(10),
            });
#pragma warning restore VSTHRD200
}
