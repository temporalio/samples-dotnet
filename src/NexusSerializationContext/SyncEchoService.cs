namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc.Handlers;

[NexusServiceHandler(typeof(ISyncEchoService))]
public class SyncEchoService
{
    [NexusOperationHandler]
    public IOperationHandler<string, string> Echo() =>
        OperationHandler.Sync<string, string>((context, message) => message);
}
