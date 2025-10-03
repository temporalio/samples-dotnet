namespace TemporalioSamples.NexusMultiArg.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(IHelloService))]
public class HelloService
{
    [NexusOperationHandler]
    public IOperationHandler<IHelloService.HelloInput, IHelloService.HelloOutput> SayHello() =>
        // This Nexus service operation is backed by a workflow run. For this sample, we are
        // altering the parameters to the workflow (in this case expanding to two parameters).
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, IHelloService.HelloInput input) =>
                context.StartWorkflowAsync(
                    (HelloHandlerWorkflow wf) => wf.RunAsync(input.Language, input.Name),
                    // Workflow IDs should typically be business meaningful IDs and are used to
                    // dedupe workflow starts. For this example, we're using the request ID
                    // allocated by Temporal when the caller workflow schedules the operation,
                    // this ID is guaranteed to be stable across retries of this operation.
                    new() { Id = context.HandlerContext.RequestId }));
}