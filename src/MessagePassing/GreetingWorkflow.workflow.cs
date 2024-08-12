namespace TemporalioSamples.MessagePassing;

using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class GreetingWorkflow
{
    public enum Language
    {
        Chinese,
        English,
        French,
        Spanish,
        Portuguese,
    }

    public record GetLanguagesInput(bool IncludeUnsupported);

    public record ApproveInput(string Name);

    private static readonly Dictionary<Language, string> Greetings = new()
    {
        [Language.English] = "Hello, world",
        [Language.Chinese] = "你好，世界",
    };

    private bool approvedForRelease;
    private string? approverName;

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        await Workflow.WaitConditionAsync(() => approvedForRelease);
        return Greetings[CurrentLanguage];
    }

    [WorkflowQuery]
    public IList<Language> GetLanguages(GetLanguagesInput input) =>
        Enum.GetValues<Language>().
            Where(language => input.IncludeUnsupported || Greetings.ContainsKey(language)).
            ToList();

    [WorkflowQuery]
    public Language CurrentLanguage { get; private set; } = GreetingWorkflow.Language.English;

    [WorkflowSignal]
    public async Task ApproveAsync(ApproveInput input)
    {
        approvedForRelease = true;
        approverName = input.Name;
    }

    [WorkflowUpdateValidator(nameof(SetCurrentLanguageAsync))]
    public void ValidateLanguage(Language language)
    {
        if (!Greetings.ContainsKey(language))
        {
            throw new ApplicationFailureException($"{language} is not supported");
        }
    }

    [WorkflowUpdate]
    public async Task<Language> SetCurrentLanguageAsync(Language language)
    {
        var previousLanguage = CurrentLanguage;
        CurrentLanguage = language;
        return previousLanguage;
    }
}