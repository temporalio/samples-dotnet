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
                    // dedupe workflow starts. For this example, use a business ID derived from
                    // the greeting input so repeated operations for the same name and language
                    // resolve to the same workflow.
                    new() { Id = GetHelloWorkflowId(input) });
            });

    private static string GetHelloWorkflowId(IHelloService.HelloInput input) =>
        $"hello-{input.Language}-{input.Name.Trim().Replace(' ', '-')}";
}
