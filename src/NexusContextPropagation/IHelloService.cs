namespace TemporalioSamples.NexusContextPropagation;

using NexusRpc;

[NexusService]
public interface IHelloService
{
    static readonly string EndpointName = "nexus-context-propagation-endpoint";

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