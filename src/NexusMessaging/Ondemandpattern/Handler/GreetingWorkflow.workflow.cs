namespace TemporalioSamples.NexusMessaging.Ondemandpattern.Handler;

using Temporalio.Exceptions;
using Temporalio.Workflows;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.Ondemandpattern;

[Workflow]
public class GreetingWorkflow
{
    private readonly Dictionary<Language, string> greetings = new()
    {
        [Language.Chinese] = "你好，世界",
        [Language.English] = "Hello, world",
    };

    private Language currentLanguage = Language.English;
    private bool approved;
    private string approvedBy = string.Empty;

    [WorkflowRun]
    public async Task<string> RunAsync(string workflowId)
    {
        // Wait for approve signal and all handlers to finish
        await Workflow.WaitConditionAsync(() => approved && Workflow.AllHandlersFinished);

        var greeting = greetings.TryGetValue(currentLanguage, out var g) ? g : "Hello, world";
        return $"{greeting} (approved by {approvedBy})";
    }

    [WorkflowQuery]
    public INexusRemoteGreetingService.GetLanguagesOutput QueryLanguages(bool includeUnsupported)
    {
        if (includeUnsupported)
        {
            return new(Enum.GetValues<Language>());
        }

        return new(greetings.Keys.ToArray());
    }

    [WorkflowQuery]
    public Language QueryLanguage() => currentLanguage;

    [WorkflowUpdate]
    public async Task<Language> SetLanguageAsync(Language newLanguage)
    {
        var prev = currentLanguage;

        // If language not yet in our greetings map, fetch from activity
        if (!greetings.ContainsKey(newLanguage))
        {
            var allGreetings = await Workflow.ExecuteActivityAsync(
                (GreetingActivities a) => a.GetAllGreetingsAsync(),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            foreach (var kvp in allGreetings)
            {
                greetings[kvp.Key] = kvp.Value;
            }
        }

        currentLanguage = newLanguage;
        return prev;
    }

    [WorkflowUpdateValidator(nameof(SetLanguageAsync))]
    public void ValidateSetLanguage(Language newLanguage)
    {
        if (!Enum.IsDefined(newLanguage))
        {
            throw new ApplicationFailureException($"Unsupported language: {newLanguage}");
        }
    }

    [WorkflowSignal]
    public async Task ApproveAsync(string name)
    {
        approved = true;
        approvedBy = name;
    }
}
