namespace TemporalioSamples.NexusMessaging.CallerPattern;

using NexusRpc;
using TemporalioSamples.NexusMessaging.Common;

[NexusService]
public interface INexusGreetingService
{
    static readonly string EndpointName = "nexus-messaging-nexus-endpoint";

    [NexusOperation]
    GetLanguagesOutput GetLanguages(GetLanguagesInput input);

    [NexusOperation]
    Language GetLanguage(GetLanguageInput input);

    [NexusOperation]
    Language SetLanguage(SetLanguageInput input);

    [NexusOperation]
    void Approve(ApproveInput input);

    public record GetLanguagesInput(bool IncludeUnsupported, string UserId);

    public record GetLanguagesOutput(IReadOnlyList<Language> Languages);

    public record GetLanguageInput(string UserId);

    public record SetLanguageInput(Language Language, string UserId);

    public record ApproveInput(string Name, string UserId);
}
