namespace TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Client;
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

    // The messaging operations are TemporalOperationHandlers: the start function receives a
    // Temporal client scoped to the invocation and returns a TemporalOperationResult. SyncResult
    // returns the value inline to the Nexus caller; starting a workflow or an update instead hands
    // back an operation token so the result is delivered over the Nexus completion callback.

    // Query: read-only, no state mutation — uses workflow query
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput> GetLanguages() =>
        TemporalOperationHandler.FromHandleFactory<INexusRemoteGreetingService.GetLanguagesInput, INexusRemoteGreetingService.GetLanguagesOutput>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                var result = await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
                return TemporalOperationResult<INexusRemoteGreetingService.GetLanguagesOutput>.SyncResult(result);
            });

    // Query: read-only — returns the workflow's current language
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.GetLanguageInput, Language> GetLanguage() =>
        TemporalOperationHandler.FromHandleFactory<INexusRemoteGreetingService.GetLanguageInput, Language>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                var result = await handle.QueryAsync(wf => wf.QueryLanguage());
                return TemporalOperationResult<Language>.SyncResult(result);
            });

    // Update: mutates state and returns the previous value — uses workflow update.
    // Starting the update through the Nexus client makes this an asynchronous Nexus operation:
    // the caller receives an operation token and the update result is delivered later over the
    // Nexus completion callback. If the update is already complete when the start call returns,
    // the result comes back synchronously instead.
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.SetLanguageInput, Language> SetLanguage() =>
        TemporalOperationHandler.FromHandleFactory<INexusRemoteGreetingService.SetLanguageInput, Language>(
            (context, client, input) =>
                client.StartWorkflowUpdateAsync<GreetingWorkflow, Language>(
                    GetWorkflowId(input.UserId),
                    wf => wf.SetLanguageAsync(input.Language),
                    new(WorkflowUpdateStage.Accepted)));

    // Signal: fire-and-forget, no return value needed — uses workflow signal
    [NexusOperationHandler]
    public IOperationHandler<INexusRemoteGreetingService.ApproveInput, NoValue> Approve() =>
        TemporalOperationHandler.FromHandleFactory<INexusRemoteGreetingService.ApproveInput, NoValue>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(GetWorkflowId(input.UserId));
                await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
                return TemporalOperationResult<NoValue>.SyncResult(default);
            });

    private static string GetWorkflowId(string userId) => $"GreetingWorkflow_for_{userId}";
}
