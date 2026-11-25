namespace TemporalioSamples.NexusSerializationContext;

using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(ISyncEchoService))]
public class SyncEchoService
{
    [NexusOperationHandler]
    public IOperationHandler<string, string> Echo() =>
        OperationHandler.Sync<string, string>((context, message) =>
        {
            var operation = NexusOperationExecutionContext.Current;
            operation.Logger.LogInformation("Handling sync echo for endpoint {Endpoint}", operation.Info.Endpoint);
            return message;
        });
}
