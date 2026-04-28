namespace TemporalioSamples.NexusSimple.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(IHelloService))]
public class HelloService
{
    [NexusOperationHandler]
    public IOperationHandler<IHelloService.EchoInput, IHelloService.EchoOutput> Echo() =>
        // This Nexus service operation is a simple sync handler
        OperationHandler.Sync<IHelloService.EchoInput, IHelloService.EchoOutput>(
            (ctx, input) => new(input.Message));

    [NexusOperationHandler]
    public IOperationHandler<IHelloService.HelloInput, IHelloService.HelloOutput> SayHello() =>
        // This Nexus service operation is backed by a workflow run
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, IHelloService.HelloInput input) =>
                context.StartWorkflowAsync(
                    (HelloHandlerWorkflow wf) => wf.RunAsync(input),
                    // Workflow IDs should typically be business meaningful IDs and are used to
                    // dedupe workflow starts. For this example, use a business ID derived from
                    // the greeting input so repeated operations for the same name and language
                    // resolve to the same workflow.
                    new() { Id = GetHelloWorkflowId(input) }));

    private static string GetHelloWorkflowId(IHelloService.HelloInput input) =>
        $"hello-{input.Language}-{input.Name.Trim().Replace(' ', '-')}";
}
