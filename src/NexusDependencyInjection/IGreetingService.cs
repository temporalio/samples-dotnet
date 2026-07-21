namespace TemporalioSamples.NexusDependencyInjection;

using NexusRpc;

[NexusService]
public interface IGreetingService
{
    [NexusOperation]
    string SayHello(SayHelloInput input);

    public record SayHelloInput(string Name);
}
