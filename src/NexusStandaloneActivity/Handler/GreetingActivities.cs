namespace TemporalioSamples.NexusStandaloneActivity.Handler;

using Temporalio.Activities;

// Activity used as the backing execution for the Nexus operation.
public static class GreetingActivities
{
    [Activity]
    public static Task<IGreetingService.GreetingOutput> CreateGreetingAsync(
        IGreetingService.GreetingInput input) =>
        Task.FromResult(new IGreetingService.GreetingOutput($"Hello, {input.Name}!"));
}
