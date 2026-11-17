namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusService("AsyncEchoService")]
public interface IAsyncEchoService
{
    [NexusOperation("echoAsync")]
    string Echo(string message);
}
