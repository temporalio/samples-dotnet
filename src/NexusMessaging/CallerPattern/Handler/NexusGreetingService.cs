namespace TemporalioSamples.NexusMessaging.CallerPattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Nexus;
using TemporalioSamples.NexusMessaging.CallerPattern;
using TemporalioSamples.NexusMessaging.Common;

// Entity pattern: the handler worker pre-starts a GreetingWorkflow per user at boot time.
// This service routes each Nexus operation to that existing workflow by deriving the
// workflow ID from the caller-supplied UserId.
[NexusServiceHandler(typeof(INexusGreetingService))]
public class NexusGreetingService
{
    // OperationHandler.Sync means the result is returned inline to the Nexus caller
    // (as opposed to WorkflowRunOperationHandler, which returns an async operation token).
    // The lambda may still be async internally.

    // Query: read-only, no state mutation — uses workflow query
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput> GetLanguages() =>
        OperationHandler.Sync<INexusGreetingService.GetLanguagesInput, INexusGreetingService.GetLanguagesOutput>(
            async (ctx, input) =>
            {
                // Access the Temporal client from the Nexus operation context
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported));
            });

    // Query: read-only — returns the workflow's current language
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.GetLanguageInput, Language> GetLanguage() =>
        OperationHandler.Sync<INexusGreetingService.GetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.QueryAsync(wf => wf.QueryLanguage());
            });

    // Update: mutates state and returns the previous value — uses workflow update
    [NexusOperationHandler]
    public IOperationHandler<INexusGreetingService.SetLanguageInput, Language> SetLanguage() =>
        OperationHandler.Sync<INexusGreetingService.SetLanguageInput, Language>(
            async (ctx, input) =>
            {
                var client = NexusOperationExecutionContext.Current.TemporalClient;
                var handle = client.GetWorkflowHandle<GreetingWorkflow>(WorkflowIdForUser(input.UserId));
                return await handle.ExecuteUpdateAsync(wf => wf.SetLanguageAsync(input.Language));
            });

    // Signal: fire-and-forget, no return value needed — uses workflow signal
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
