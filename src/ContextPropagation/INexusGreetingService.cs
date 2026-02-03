namespace TemporalioSamples.ContextPropagation;

using NexusRpc;

[NexusService]
public interface INexusGreetingService
{
    static readonly string EndpointName = "context-propagation-greeting-service";

    [NexusOperation]
    GreetingOutput SayGreeting(GreetingInput input);

    public record GreetingInput(string Name);

    public record GreetingOutput(string Message);
}
