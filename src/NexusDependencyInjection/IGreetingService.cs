namespace TemporalioSamples.NexusDependencyInjection;

using NexusRpc;

[NexusService]
public interface IGreetingService
{
    static readonly string EndpointName = "my-nexus-endpoint";

    [NexusOperation]
    string SayHello(SayHelloInput input);

    public record SayHelloInput(string Name);
}
