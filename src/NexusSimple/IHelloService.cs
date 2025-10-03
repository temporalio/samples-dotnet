namespace TemporalioSamples.NexusSimple;

using NexusRpc;

[NexusService]
public interface IHelloService
{
    static readonly string EndpointName = "nexus-simple-endpoint";

    [NexusOperation]
    EchoOutput Echo(EchoInput input);

    [NexusOperation]
    HelloOutput SayHello(HelloInput input);

    public record EchoInput(string Message);

    public record EchoOutput(string Message);

    public record HelloInput(string Name, HelloLanguage Language);

    public record HelloOutput(string Message);

    public enum HelloLanguage
    {
        En,
        Fr,
        De,
        Es,
        Tr,
    }
}
