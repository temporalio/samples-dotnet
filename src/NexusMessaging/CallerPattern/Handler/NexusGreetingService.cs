namespace TemporalioSamples.NexusMessaging.CallerPattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Nexus;
using TemporalioSamples.NexusMessaging.CallerPattern;
using TemporalioSamples.NexusMessaging.Common;

// Entity pattern: the handler worker pre-starts a GreetingWorkflow per user at boot time.
// This service routes each Nexus operation to that existing workflow by deriving the
// workflow ID from the caller-supplied UserId.
[NexusServiceHandler(typeof(INexusGreetingService))]
public class NexusGreetingService
{
    // Every operation is a TemporalOperationHandler: the start function receives a Temporal
    // client scoped to the invocation and returns a TemporalOperationResult. SyncResult returns
    // the value inline to the Nexus caller; starting a workflow or an update instead hands back
    // an operation token so the result is delivered over the Nexus completion callback.

    // Query: read-only, no state mutation — uses workflow query
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput> GetLanguages() =>
        TemporalOperationHandler.FromHandleFactory<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                var result = await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
                return TemporalOperationResult<INexusGreetingService.GetLanguagesOutput>.SyncResult(result);
            });

    // Query: read-only — returns the workflow's current language
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguageInput, Language> GetLanguage() =>
        TemporalOperationHandler.FromHandleFactory<INexusGreetingService.GetLanguageInput, Language>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                var result = await handle.QueryAsync(wf => wf.QueryLanguage());
                return TemporalOperationResult<Language>.SyncResult(result);
            });

    // Update: mutates state and returns the previous value — uses workflow update.
    // Starting the update through the Nexus client makes this an asynchronous Nexus operation:
    // the caller receives an operation token and the update result is delivered later over the
    // Nexus completion callback. If the update is already complete when the start call returns,
    // the result comes back synchronously instead.
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.SetLanguageInput, Language> SetLanguage() =>
        TemporalOperationHandler.FromHandleFactory<INexusGreetingService.SetLanguageInput, Language>(
            (context, client, input) =>
                client.StartWorkflowUpdateAsync<GreetingWorkflow, Language>(
                    WorkflowIdForUser(input.UserId),
                    wf => wf.SetLanguageAsync(input.Language),
                    new(WorkflowUpdateStage.Accepted)));

    // Signal: fire-and-forget, no return value needed — uses workflow signal
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.ApproveInput, NoValue> Approve() =>
        TemporalOperationHandler.FromHandleFactory<INexusGreetingService.ApproveInput, NoValue>(
            async (context, client, input) =>
            {
                var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
                return TemporalOperationResult<NoValue>.SyncResult(default);
            });

    private static string WorkflowIdForUser(string userId) => $"GreetingWorkflow_for_{userId}";
}
