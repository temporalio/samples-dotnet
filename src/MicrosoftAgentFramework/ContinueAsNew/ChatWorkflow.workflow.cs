// @@@SNIPSTART dotnet-microsoft-agent-framework-continue-as-new-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;

using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Workflows;

public record ChatInput(JsonElement? Session = null);

[Workflow]
public class ChatWorkflow
{
    private readonly Semaphore turnLock = new(1);
    private TemporalAgent? agent;
    private AgentSession? session;
    private bool done;

    [WorkflowUpdate]
    public async Task<string> TurnAsync(string prompt)
    {
        await Workflow.WaitConditionAsync(() => session is not null);
        // One turn at a time, so the session's history stays in order.
        await turnLock.WaitAsync();
        try
        {
            var response = await agent!.RunAsync(prompt, session);
            return response.Text;
        }
        finally
        {
            turnLock.Release();
        }
    }

    [WorkflowSignal]
    public async Task EndChatAsync() => done = true;

    [WorkflowQuery]
    public IReadOnlyList<string> Transcript() =>
        agent?.ChatHistoryProvider is InMemoryChatHistoryProvider history && session is not null
            ? history.GetMessages(session).Select(message => $"{message.Role}: {message.Text}").ToList()
            : Array.Empty<string>();

    [WorkflowRun]
    public async Task RunAsync(ChatInput input)
    {
        agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions { Instructions = "You are a helpful assistant. Keep replies short." },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });
        // The serialized session carries the transcript across the Continue-As-New boundary. The
        // agent object does not cross and is built again here.
        session = input.Session is { } carried
            ? await agent.DeserializeSessionAsync(carried)
            : await agent.CreateSessionAsync();

        await Workflow.WaitConditionAsync(() => done || Workflow.ContinueAsNewSuggested);
        // Let in-flight turns finish before the handoff.
        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);
        if (!done)
        {
            var next = new ChatInput(await agent.SerializeSessionAsync(session));
            throw Workflow.CreateContinueAsNewException((ChatWorkflow wf) => wf.RunAsync(next));
        }
    }
}
// @@@SNIPEND
