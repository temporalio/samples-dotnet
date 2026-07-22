namespace TemporalioSamples.NexusMessaging.CallerPattern.Caller;

using Temporalio.Workflows;
using TemporalioSamples.NexusMessaging.CallerPattern;
using TemporalioSamples.NexusMessaging.Common;

[Workflow]
public class CallerWorkflow
{
    [WorkflowRun]
    public async Task<string[]> RunAsync(string userId)
    {
        var log = new List<string>();
        var client = Workflow.CreateNexusWorkflowClient<INexusGreetingService>(
            NexusEndpoints.GreetingService);

        // GetLanguages - query entity workflow for supported languages
        var languagesOutput = await client.ExecuteNexusOperationAsync(
            svc => svc.GetLanguages(new INexusGreetingService.GetLanguagesInput(false, userId)));
        log.Add($"Supported languages: {string.Join(", ", languagesOutput.Languages)}");

        // GetLanguage - query entity workflow for current language
        var currentLanguage = await client.ExecuteNexusOperationAsync(
            svc => svc.GetLanguage(new INexusGreetingService.GetLanguageInput(userId)));
        log.Add($"Current language: {currentLanguage}");

        // SetLanguage - update entity workflow to change language
        var previousLanguage = await client.ExecuteNexusOperationAsync(
            svc => svc.SetLanguage(new INexusGreetingService.SetLanguageInput(Language.Chinese, userId)));
        log.Add($"Set language from {previousLanguage} to {Language.Chinese}");

        // Approve - signal entity workflow to complete
        await client.ExecuteNexusOperationAsync(
            svc => svc.Approve(new INexusGreetingService.ApproveInput("CallerWorkflow", userId)));
        log.Add("Approved workflow");

        return log.ToArray();
    }
}
