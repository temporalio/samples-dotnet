namespace TemporalioSamples.NexusMultiArg;

using NexusRpc;

[NexusService]
public interface IHelloService
{
    static readonly string EndpointName = "nexus-multi-arg-endpoint";

    [NexusOperation]
    HelloOutput SayHello(HelloInput input);

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
