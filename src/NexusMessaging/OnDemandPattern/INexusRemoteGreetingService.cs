namespace TemporalioSamples.NexusMessaging.OnDemandPattern;

using NexusRpc;
using TemporalioSamples.NexusMessaging.Common;

[NexusService]
public interface INexusRemoteGreetingService
{
    static readonly string EndpointName = "nexus-messaging-on-demand-pattern-endpoint";

    [NexusOperation]
    string RunFromRemote(RunFromRemoteInput input);

    [NexusOperation]
    GetLanguagesOutput GetLanguages(GetLanguagesInput input);

    [NexusOperation]
    Language GetLanguage(GetLanguageInput input);

    [NexusOperation]
    Language SetLanguage(SetLanguageInput input);

    [NexusOperation]
    void Approve(ApproveInput input);

    public record RunFromRemoteInput(string UserId);

    public record GetLanguagesInput(bool IncludeUnsupported, string UserId);

    public record GetLanguagesOutput(IReadOnlyList<Language> Languages);

    public record GetLanguageInput(string UserId);

    public record SetLanguageInput(Language Language, string UserId);

    public record ApproveInput(string Name, string UserId);
}
