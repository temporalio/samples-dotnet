namespace TemporalioSamples.NexusStandaloneActivity;

using NexusRpc;

// Nexus service definition shared by the caller and the handler.
[NexusService]
public interface IGreetingService
{
    [NexusOperation]
    GreetingOutput Greet(GreetingInput input);

    public record GreetingInput(string Name);

    public record GreetingOutput(string Message);
}
