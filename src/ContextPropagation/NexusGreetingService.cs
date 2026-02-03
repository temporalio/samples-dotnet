namespace TemporalioSamples.ContextPropagation;

using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Nexus;

[NexusServiceHandler(typeof(INexusGreetingService))]
public class NexusGreetingService
{
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GreetingInput, INexusGreetingService.GreetingOutput> SayGreeting() =>
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, INexusGreetingService.GreetingInput input) =>
            {
                // Log context to show it was propagated to the handler
                NexusOperationExecutionContext.Current.Logger.LogInformation(
                    "Nexus greeting service called by user {UserId}", MyContext.UserId);

                return context.StartWorkflowAsync(
                    (NexusGreetingHandlerWorkflow wf) => wf.RunAsync(input),
                    new() { Id = context.HandlerContext.RequestId });
            });
}
