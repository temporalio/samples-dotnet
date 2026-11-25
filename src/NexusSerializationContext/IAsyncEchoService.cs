namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc;

[NexusService("AsyncEchoService")]
public interface IAsyncEchoService
{
    [NexusOperation("echoAsync")]
    string Echo(string message);
}
