namespace TemporalioSamples.NexusMultiArg;

using NexusRpc;

[NexusService]
public interface IHelloService
{
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
