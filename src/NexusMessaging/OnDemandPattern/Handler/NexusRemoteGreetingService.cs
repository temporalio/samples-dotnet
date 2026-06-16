namespace TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern;

// On-demand pattern: no workflow is pre-started. The caller creates workflow instances
// through Nexus operations. Each operation includes a UserId so the handler can derive
// the target workflow ID.
[NexusServiceHandler(typeof(INexusRemoteGreetingService))]
public class NexusRemoteGreetingService
{
    // WorkflowRunOperationHandler starts a backing workflow and returns its handle to the
    // Nexus infrastructure. The caller receives an async operation token and can poll for
    // the workflow result later via GetResultAsync.
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.RunFromRemoteInput, string> RunFromRemote() =>
        WorkflowRunOperationHandler.FromHandleFactory(
            (WorkflowRunOperationContext context, INexusRemoteGreetingService.RunFromRemoteInput input) =>
                context.StartWorkflowAsync(
                    (GreetingWorkflow wf) => wf.RunAsync(input.UserId),
                    new() { Id = GetWorkflowId(input.UserId) }));

    // OperationHandler.Sync means the result is returned inline to the Nexus caller
    // (as opposed to WorkflowRunOperationHandler, which returns an async operation token).
    // The lambda may still be async internally.

    // Query: read-only, no state mutation — uses workflow query
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput> GetLanguages() =>
        OperationHandler.Sync<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput>(
            async (ctx, input) =>
            {
                // Access the Temporal client from the Nexus operation context
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
            });

    // Query: read-only — returns the workflow's current language
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguageInput, Language> GetLanguage() =>
        OperationHandler.Sync<INexusRemoteGreetingService.GetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguage());
            });

    // Update: mutates state and returns the previous value — uses workflow update
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.SetLanguageInput, Language> SetLanguage() =>
        OperationHandler.Sync<INexusRemoteGreetingService.SetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                return await handle.ExecuteUpdateAsync(wf => wf.SetLanguageAsync(input.Language));
            });

    // Signal: fire-and-forget, no return value needed — uses workflow signal
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
