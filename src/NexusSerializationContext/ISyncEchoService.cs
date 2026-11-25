namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc;

[NexusService("SyncEchoService")]
public interface ISyncEchoService
{
    [NexusOperation("echo")]
    string Echo(string message);
}
