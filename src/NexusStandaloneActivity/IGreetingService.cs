namespace TemporalioSamples.NexusStandaloneActivity;

using NexusRpc;

// Nexus service definition shared by the caller and the handler. It declares a single operation
// whose backing execution is a Standalone Activity.
[NexusService]
public interface IGreetingService
{
    [NexusOperation]
    GreetingOutput Greet(GreetingInput input);

    public record GreetingInput(string Name);

    public record GreetingOutput(string Message);
}
