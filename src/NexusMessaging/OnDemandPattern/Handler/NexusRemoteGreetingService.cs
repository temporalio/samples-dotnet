namespace TemporalioSamples.NexusMessaging.OnDemandPattern.Handler;

using NexusRpc.Handlers;
using Temporalio.Api.Enums.V1;
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
#pragma warning disable VSTHRD200 // Names must match the INexusRemoteGreetingService operations, which can't take the Async suffix

    // Starts the GreetingWorkflow for the given user, or attaches to one already running.
    // StartWorkflowAsync returns an async result, which attaches a completion callback,
    // so the Operation completes when the Workflow returns.
    [TemporalOperation]
    public Task<TemporalOperationResult<string>> RunFromRemote(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusRemoteGreetingService.RunFromRemoteInput input) =>
        client.StartWorkflowAsync(
            (GreetingWorkflow wf) => wf.RunAsync(input.UserId),
            new()
            {
                Id = GetWorkflowId(input.UserId),

                // By default, starting a Workflow whose ID is already running fails the Operation.
                // Since AttachApprovalContext below can create the GreetingWorkflow first, this
                // Operation needs to attach to the running execution rather than fail.
                IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            });

    // Query: read-only, no state mutation — uses workflow query
    [TemporalOperation]
    public async Task<TemporalOperationResult<INexusRemoteGreetingService.GetLanguagesOutput>> GetLanguages(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusRemoteGreetingService.GetLanguagesInput input)
    {
        // Access the Temporal client from the Nexus client passed to the handler
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            GetWorkflowId(input.UserId));
        return TemporalOperationResult<INexusRemoteGreetingService.GetLanguagesOutput>.SyncResult(
            await handle.QueryAsync(wf => wf.QueryLanguages(input.IncludeUnsupported)));
    }

    // Query: read-only — returns the workflow's current language
    [TemporalOperation]
    public async Task<TemporalOperationResult<Language>> GetLanguage(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusRemoteGreetingService.GetLanguageInput input)
    {
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            GetWorkflowId(input.UserId));
        return TemporalOperationResult<Language>.SyncResult(
            await handle.QueryAsync(wf => wf.QueryLanguage()));
    }

    // Update: mutates state and returns the previous value — uses workflow update
    [TemporalOperation]
    public Task<TemporalOperationResult<Language>> SetLanguage(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusRemoteGreetingService.SetLanguageInput input) =>
        client.StartWorkflowUpdateAsync<GreetingWorkflow, Language>(
            GetWorkflowId(input.UserId),
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
        INexusRemoteGreetingService.ApproveInput input)
    {
        var handle = client.TemporalClient.GetWorkflowHandle<GreetingWorkflow>(
            GetWorkflowId(input.UserId));
        await handle.SignalAsync(wf => wf.ApproveAsync(input.Name));
        return TemporalOperationResult<NoValue>.SyncResult(default);
    }

    // Signal-with-Start: starts the Workflow first if it is not already running.
    // When the Workflow already exists, only the Signal is delivered.
    [TemporalOperation]
    public async Task<TemporalOperationResult<NoValue>> AttachApprovalContext(
        TemporalOperationStartContext ctx,
        ITemporalNexusClient client,
        INexusRemoteGreetingService.AttachApprovalContextInput input)
    {
        var options = new WorkflowOptions(
            id: GetWorkflowId(input.UserId),
            taskQueue: NexusOperationExecutionContext.Current.Info.TaskQueue);
        options.SignalWithStart((GreetingWorkflow wf) => wf.AttachApprovalContextAsync(input.Note));
        await client.TemporalClient.StartWorkflowAsync(
            (GreetingWorkflow wf) => wf.RunAsync(input.UserId), options);
        return TemporalOperationResult<NoValue>.SyncResult(default);
    }

#pragma warning restore VSTHRD200

    private static string GetWorkflowId(string userId) => $"GreetingWorkflow_for_{userId}";
}
