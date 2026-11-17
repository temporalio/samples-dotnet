namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusService("SyncEchoService")]
public interface ISyncEchoService
{
    [NexusOperation("echo")]
    string Echo(string message);
}
