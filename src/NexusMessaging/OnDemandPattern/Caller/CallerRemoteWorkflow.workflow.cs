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
            INexusRemoteGreetingService.EndpointName);

        // Start two remote greeting workflows on demand
        var userIdOne = "user-one";
        var userIdTwo = "user-two";

        var handleOne = await client.StartNexusOperationAsync(
            svc => svc.RunFromRemote(new INexusRemoteGreetingService.RunFromRemoteInput(userIdOne)));
        log.Add($"Started remote workflow for user: {userIdOne}");

        var handleTwo = await client.StartNexusOperationAsync(
            svc => svc.RunFromRemote(new INexusRemoteGreetingService.RunFromRemoteInput(userIdTwo)));
        log.Add($"Started remote workflow for user: {userIdTwo}");

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
