namespace TemporalioSamples.NexusSerializationContext;

using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(IAsyncEchoService))]
public class AsyncEchoService
{
    [NexusOperationHandler]
    public IOperationHandler<string, string> Echo() =>
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, string message) =>
            {
                var operation = NexusOperationExecutionContext.Current;
                operation.Logger.LogInformation("Starting async echo for endpoint {Endpoint}", operation.Info.Endpoint);
                return context.StartWorkflowAsync(
                    (EchoWorkflow workflow) => workflow.RunAsync(message),
                    new() { Id = $"nexus-echo-{context.HandlerContext.RequestId}" });
            });
}
