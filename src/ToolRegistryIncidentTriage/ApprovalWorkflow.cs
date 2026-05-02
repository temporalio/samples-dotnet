using Temporalio.Workflows;

namespace TemporalioSamples.ToolRegistryIncidentTriage;

/// <summary>
/// Companion HITL workflow.
///
/// The triage agent's request_human_approval tool calls SignalWithStartWorkflow
/// against a deterministic ID per alert group. This workflow stores the latest
/// request, exposes it as a query, and returns the operator's decision.
/// </summary>
[Workflow("approvalWorkflow")]
public class ApprovalWorkflow
{
    private ApprovalRequest? request;
    private ApprovalResponse? response;

    [WorkflowRun]
    public async Task<ApprovalResponse> RunAsync(string key)
    {
        // LLM retry: re-attached requests overwrite. Operator only ever
        // sees the latest version, since the agent may refine its ask.
        await Workflow.WaitConditionAsync(() => request != null);
        await Workflow.WaitConditionAsync(() => response != null);
        return response!;
    }

    [WorkflowSignal("approval-request")]
    public Task ApprovalRequestAsync(ApprovalRequest req)
    {
        request = req;
        return Task.CompletedTask;
    }

    [WorkflowSignal("approval-decision")]
    public Task ApprovalDecisionAsync(ApprovalResponse res)
    {
        response = res;
        return Task.CompletedTask;
    }

    [WorkflowQuery("pending-approval")]
    public ApprovalRequest? PendingApproval() => request;
}
