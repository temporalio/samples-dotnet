namespace TemporalioSamples.NexusDependencyInjection.Handler;

// A dependency to be injected into the Nexus service handler. In a real application this might be a
// highly reliable dependency like a database client, an HTTP client, or some other service.
public interface IGreetingClient
{
    Task<string> GetGreetingAsync(string name);
}
