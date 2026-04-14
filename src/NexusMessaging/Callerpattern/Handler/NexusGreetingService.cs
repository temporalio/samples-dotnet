namespace TemporalioSamples.NexusMessaging.Callerpattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;
using TemporalioSamples.NexusMessaging.Callerpattern;
using TemporalioSamples.NexusMessaging.Common;

[NexusServiceHandler(typeof(INexusGreetingService))]
public class NexusGreetingService
{
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput> GetLanguages() =>
        OperationHandler.Sync<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguageInput, Language> GetLanguage() =>
        OperationHandler.Sync<INexusGreetingService.GetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguage());
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.SetLanguageInput, Language> SetLanguage() =>
        OperationHandler.Sync<INexusGreetingService.SetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.ExecuteUpdateAsync(wf => wf.SetLanguageAsync(input.Language));
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.ApproveInput, NoValue> Approve() =>
        OperationHandler.Sync<INexusGreetingService.ApproveInput, NoValue>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
                return default;
            });

    private static string WorkflowIdForUser(string userId) => $"GreetingWorkflow_for_{userId}";
}
