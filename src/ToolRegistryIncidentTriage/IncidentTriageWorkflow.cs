using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.ToolRegistryIncidentTriage;

[Workflow("incidentTriageWorkflow")]
public class IncidentTriageWorkflow
{
    private AlertPayload? currentAlert;
    private TriageResult? result;

    [WorkflowRun]
    public async Task<TriageResult> RunAsync(AlertPayload initialAlert)
    {
        currentAlert = initialAlert;
        // agenticHitl-shaped timeouts (matches lexicon-temporal's profile).
        result = await Workflow.ExecuteActivityAsync(
            (TriageActivity a) => a.TriageIncidentAsync(currentAlert),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromHours(8),
                HeartbeatTimeout = TimeSpan.FromSeconds(120),
                RetryPolicy = new RetryPolicy { MaximumAttempts = 1 },
            });
        return result;
    }

    [WorkflowSignal("alert-update")]
    public Task AlertUpdateAsync(AlertPayload alert)
    {
        currentAlert = alert;
        return Task.CompletedTask;
    }

    [WorkflowQuery("current-alert")]
    public AlertPayload? CurrentAlert() => currentAlert;

    [WorkflowQuery("triage-result")]
    public TriageResult? TriageResultQuery() => result;
}
