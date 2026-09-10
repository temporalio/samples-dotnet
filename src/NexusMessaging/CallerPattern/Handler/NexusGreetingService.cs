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
#pragma warning disable VSTHRD200 // Names must match the INexusGreetingService operations, which can't take the Async suffix

    // Query: read-only, no state mutation — uses workflow query
    [TemporalOperation]
    public async Task<TemporalOperationResult<INexusGreetingService.GetLanguagesOutput>> GetLanguages(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusGreetingService.GetLanguagesInput input)
    {
        // Access the Temporal client from the Nexus client passed to the handler
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            WorkflowIdForUser(input.UserId));
        return TemporalOperationResult<INexusGreetingService.GetLanguagesOutput>.SyncResult(
            await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported)));
    }

    // Query: read-only — returns the workflow's current language
    [TemporalOperation]
    public async Task<TemporalOperationResult<Language>> GetLanguage(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusGreetingService.GetLanguageInput input)
    {
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            WorkflowIdForUser(input.UserId));
        return TemporalOperationResult<Language>.SyncResult(
            await handle.QueryAsync(wf => wf.QueryLanguage()));
    }

    // Update: mutates state and returns the previous value — uses workflow update
    [TemporalOperation]
    public Task<TemporalOperationResult<Language>> SetLanguage(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusGreetingService.SetLanguageInput input) =>
        client.StartWorkflowUpdateAsync<GreetingWorkflow, Language>(
            WorkflowIdForUser(input.UserId),
            wf => wf.SetLanguageAsync(input.Language),
            // An Update-backed Operation must wait for the Accepted stage. Any other stage is
            // rejected with "nexus op workflow updates only support WorkflowUpdateStageAccepted
            // for async updates".
            new(WorkflowUpdateStage.Accepted));

    // Signal: fire-and-forget, no return value needed — uses workflow signal
    [TemporalOperation]
    public async Task<TemporalOperationResult<NoValue>> Approve(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusGreetingService.ApproveInput input)
    {
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            WorkflowIdForUser(input.UserId));
        await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
        return TemporalOperationResult<NoValue>.SyncResult(default);
    }

#pragma warning restore VSTHRD200

    private static string WorkflowIdForUser(string userId) => $"GreetingWorkflow_for_{userId}";
}
