namespace TemporalioSamples.NexusMessaging.OnDemandPattern.Caller;

using Temporalio.Workflows;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern;

[Workflow]
public class CallerRemoteWorkflow
{
    [WorkflowRun]
    public async Task<string[]> RunAsync()
    {
        var log = new List<string>();
        var client = Workflow.CreateNexusWorkflowClient<INexusRemoteGreetingService>(
            NexusEndpoints.RemoteGreetingService);

        // Start two remote greeting workflows on demand
        var userIdOne = "user-one";
        var userIdTwo = "user-two";

        // Attach information before the Workflow exists. Since AttachApprovalContext is backed
        // by Signal-with-Start on the handler, this call creates the Workflow and delivers the
        // note to it.
        await client.ExecuteNexusOperationAsync(
            svc => svc.AttachApprovalContext(new INexusRemoteGreetingService.AttachApprovalContextInput(
                "queued for localization review by the nightly batch", userIdOne)));
        log.Add($"Attached approval context before the workflow existed: {userIdOne}");

        // The Workflow for this user is already running due to AttachApprovalContext. The handler
        // sets the conflict policy to UseExisting, so this call attaches the Operation's
        // completion callback to the running execution.
        var handleOne = await client.StartNexusOperationAsync(
            svc => svc.RunFromRemote(new INexusRemoteGreetingService.RunFromRemoteInput(userIdOne)));
        log.Add($"Started remote workflow for user: {userIdOne}");

        var handleTwo = await client.StartNexusOperationAsync(
            svc => svc.RunFromRemote(new INexusRemoteGreetingService.RunFromRemoteInput(userIdTwo)));
        log.Add($"Started remote workflow for user: {userIdTwo}");

        // This user's Workflow was created by RunFromRemote just above, so here Signal-with-Start
        // skips the start and only delivers the Signal.
        await client.ExecuteNexusOperationAsync(
            svc => svc.AttachApprovalContext(new INexusRemoteGreetingService.AttachApprovalContextInput(
                "translation approved by the localization team", userIdTwo)));
        log.Add($"Attached approval context to the running workflow: {userIdTwo}");

        // Interact with workflow one: get languages, set language, approve
        var languagesOne = await client.ExecuteNexusOperationAsync(
            svc => svc.GetLanguages(new INexusRemoteGreetingService.GetLanguagesInput(false, userIdOne)));
        log.Add($"[One] Supported languages: {string.Join(", ", languagesOne.Languages)}");

        var prevLangOne = await client.ExecuteNexusOperationAsync(
            svc => svc.SetLanguage(new INexusRemoteGreetingService.SetLanguageInput(Language.Spanish, userIdOne)));
        log.Add($"[One] Set language from {prevLangOne} to {Language.Spanish}");

        await client.ExecuteNexusOperationAsync(
            svc => svc.Approve(new INexusRemoteGreetingService.ApproveInput("CallerRemoteWorkflow", userIdOne)));
        log.Add("[One] Approved");

        // Interact with workflow two: get language, set language, approve
        var currentLangTwo = await client.ExecuteNexusOperationAsync(
            svc => svc.GetLanguage(new INexusRemoteGreetingService.GetLanguageInput(userIdTwo)));
        log.Add($"[Two] Current language: {currentLangTwo}");

        var prevLangTwo = await client.ExecuteNexusOperationAsync(
            svc => svc.SetLanguage(new INexusRemoteGreetingService.SetLanguageInput(Language.French, userIdTwo)));
        log.Add($"[Two] Set language from {prevLangTwo} to {Language.French}");

        await client.ExecuteNexusOperationAsync(
            svc => svc.Approve(new INexusRemoteGreetingService.ApproveInput("CallerRemoteWorkflow", userIdTwo)));
        log.Add("[Two] Approved");

        // Wait for both remote workflows to complete
        var resultOne = await handleOne.GetResultAsync();
        log.Add($"[One] Result: {resultOne}");

        var resultTwo = await handleTwo.GetResultAsync();
        log.Add($"[Two] Result: {resultTwo}");

        return log.ToArray();
    }
}
