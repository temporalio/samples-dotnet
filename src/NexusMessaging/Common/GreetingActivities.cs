namespace TemporalioSamples.NexusMessaging.Common;

using Temporalio.Activities;

public class GreetingActivities
{
    [Activity]
    public Task<Dictionary<Language, string>> GetAllGreetingsAsync() =>
        Task.FromResult(new Dictionary<Language, string>
        {
            [Language.Arabic] = "مرحبا بالعالم",
            [Language.Chinese] = "你好，世界",
            [Language.English] = "Hello, world",
            [Language.French] = "Bonjour, monde",
            [Language.Hindi] = "नमस्ते दुनिया",
            [Language.Portuguese] = "Olá, mundo",
            [Language.Spanish] = "Hola, mundo",
        });
}
