namespace TemporalioSamples.NexusSerializationContext;

using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(IAsyncEchoService))]
public class AsyncEchoService
{
    [NexusOperationHandler]
    public IOperationHandler<string, string> Echo() =>
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, string message) => context.StartWorkflowAsync(
                (EchoWorkflow workflow) => workflow.RunAsync(message),
                // Request IDs survive retries and isolate independent operations, even for identical input.
                new() { Id = $"nexus-echo-{context.HandlerContext.RequestId}" }));
}
