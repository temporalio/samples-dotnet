// @@@SNIPSTART dotnet-microsoft-agent-framework-middleware-middleware
namespace TemporalioSamples.MicrosoftAgentFramework.Middleware;

using Microsoft.Agents.AI;

// Adds one sentence to the instructions of every model call. Context providers run in the
// Workflow around each run and must complete synchronously; work that needs I/O belongs in an
// Activity whose result you add to the prompt.
public sealed class AppendReminder : AIContextProvider
{
    public const string Reminder = "Answer in one short sentence.";

    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        new(new AIContext { Instructions = Reminder });
}
// @@@SNIPEND
