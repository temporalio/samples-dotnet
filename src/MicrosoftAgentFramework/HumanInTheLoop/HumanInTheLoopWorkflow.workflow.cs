// @@@SNIPSTART dotnet-microsoft-agent-framework-human-in-the-loop-workflow
namespace TemporalioSamples.MicrosoftAgentFramework.HumanInTheLoop;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Temporalio.Exceptions;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Workflows;

public record ApprovalRequest(string RequestId, string ToolName, IReadOnlyDictionary<string, object?> Arguments);

public record Decision(string RequestId, bool Approved);

[Workflow]
public class HumanInTheLoopWorkflow
{
    private readonly Dictionary<string, ToolApprovalRequestContent> pending = new();
    private readonly Dictionary<string, bool> decisions = new();
    private bool awaitingReply;

    [WorkflowQuery]
    public IReadOnlyList<ApprovalRequest> PendingApprovals() => pending.Values.Select(ToApprovalRequest).ToList();

    [WorkflowUpdateValidator(nameof(DecideAsync))]
    public void ValidateDecision(Decision decision)
    {
        // Validators run before the Workflow sees the model's reply, so this one checks the input
        // and not the state. Bad input never enters history.
        if (string.IsNullOrEmpty(decision.RequestId))
        {
            throw new ApplicationFailureException("RequestId is required");
        }
    }

    [WorkflowUpdate]
    public async Task<string> DecideAsync(Decision decision)
    {
        // A decision can arrive in the same Workflow Task as the model's reply, before RunAsync has
        // read the requests in that reply. Judge the id once the reply has been read.
        await Workflow.WaitConditionAsync(() => !awaitingReply);
        if (decisions.ContainsKey(decision.RequestId))
        {
            throw new ApplicationFailureException($"Approval request '{decision.RequestId}' is already decided");
        }

        if (!pending.ContainsKey(decision.RequestId))
        {
            throw new ApplicationFailureException($"No pending approval request '{decision.RequestId}'");
        }

        decisions[decision.RequestId] = decision.Approved;
        return decision.Approved ? "approved" : "rejected";
    }

    [WorkflowRun]
    public async Task<string> RunAsync(string prompt)
    {
        // ApprovalRequiredAIFunction is Agent Framework's own approval wrapper. The run returns a
        // ToolApprovalRequestContent instead of calling the tool.
        var deleteFile = AIFunctionFactory.Create(
            (string path) => $"deleted {path}",
            "delete_file",
            "Deletes a file. Runs only after a person approves.");
        var agent = new TemporalAgent("gpt-4o-mini", new TemporalAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You manage files. Use delete_file when asked to delete one.",
                Tools = new List<AITool> { new ApprovalRequiredAIFunction(deleteFile) },
            },
            ModelActivityOptions = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(60) },
        });

        // Approvals are resolved against the session, so every run uses the same one.
        var session = await agent.CreateSessionAsync();
        var response = await ReplyAsync(agent, new ChatMessage(ChatRole.User, prompt), session);
        var requests = ApprovalRequests(response);
        while (requests.Count > 0)
        {
            foreach (var request in requests)
            {
                pending[request.RequestId] = request;
            }

            // No Activity is open while the Workflow waits: the wait costs nothing and survives a
            // worker restart.
            await Workflow.WaitConditionAsync(() => pending.Keys.All(decisions.ContainsKey));
            var replies = requests.Select(request => (AIContent)request.CreateResponse(decisions[request.RequestId])).ToList();
            pending.Clear();
            response = await ReplyAsync(agent, new ChatMessage(ChatRole.User, replies), session);
            requests = ApprovalRequests(response);
        }

        return response.Text;
    }

    private static List<ToolApprovalRequestContent> ApprovalRequests(AgentResponse response) =>
        response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();

    private static ApprovalRequest ToApprovalRequest(ToolApprovalRequestContent request)
    {
        var call = request.ToolCall as FunctionCallContent;
        var arguments = call?.Arguments is { } values
            ? new Dictionary<string, object?>(values)
            : new Dictionary<string, object?>();
        return new ApprovalRequest(request.RequestId, call?.Name ?? string.Empty, arguments);
    }

    private async Task<AgentResponse> ReplyAsync(TemporalAgent agent, ChatMessage message, AgentSession session)
    {
        awaitingReply = true;
        try
        {
            return await agent.RunAsync(message, session);
        }
        finally
        {
            awaitingReply = false;
        }
    }
}
// @@@SNIPEND
