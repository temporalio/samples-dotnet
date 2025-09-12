namespace TemporalioSamples.NexusContextPropagation.Handler;

using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Nexus;
using TemporalioSamples.ContextPropagation;

[NexusServiceHandler(typeof(IHelloService))]
public class HelloService
{
    [NexusOperationHandler]
    public IOperationHandler<IHelloService.HelloInput, IHelloService.HelloOutput> SayHello() =>
        // This Nexus service operation is backed by a workflow run
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, IHelloService.HelloInput input) =>
            {
                NexusOperationExecutionContext.Current.Logger.LogInformation(
                    "Hello service called by user {UserId}", MyContext.UserId);
                return context.StartWorkflowAsync(
                    (HelloHandlerWorkflow wf) => wf.RunAsync(input),
                    // Workflow IDs should typically be business meaningful IDs and are used to
                    // dedupe workflow starts. For this example, we're using the request ID
                    // allocated by Temporal when the caller workflow schedules the operation,
                    // this ID is guaranteed to be stable across retries of this operation.
                    new() { Id = context.HandlerContext.RequestId });
            });
}