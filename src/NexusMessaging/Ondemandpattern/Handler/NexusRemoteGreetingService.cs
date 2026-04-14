namespace TemporalioSamples.NexusMessaging.Ondemandpattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.Ondemandpattern;

[NexusServiceHandler(typeof(INexusRemoteGreetingService))]
public class NexusRemoteGreetingService
{
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.RunFromRemoteInput, string> RunFromRemote() =>
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, INexusRemoteGreetingService.RunFromRemoteInput input) =>
                context.StartWorkflowAsync(
                    (GreetingWorkflow wf) => wf.RunAsync(GetWorkflowId(input.UserId)),
                    new() { Id = GetWorkflowId(input.UserId) }));

    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput> GetLanguages() =>
        OperationHandler.Sync<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguageInput, Language> GetLanguage() =>
        OperationHandler.Sync<INexusRemoteGreetingService.GetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguage());
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.SetLanguageInput, Language> SetLanguage() =>
        OperationHandler.Sync<INexusRemoteGreetingService.SetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.ExecuteUpdateAsync(wf => wf.SetLanguageAsync(input.Language));
            });

    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.ApproveInput, NoValue> Approve() =>
        OperationHandler.Sync<INexusRemoteGreetingService.ApproveInput, NoValue>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
                return default;
            });

    private static string GetWorkflowId(string userId) => $"GreetingWorkflow_for_{userId}";
}
