namespace TemporalioSamples.NexusDependencyInjection.Handler;

public class GreetingClient : IGreetingClient
{
    public Task<string> GetGreetingAsync(string name) =>
        Task.FromResult($"Hello, {name} 👋");
}
