namespace TemporalioSamples.UpdateWithStartEarlyReturn;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class PaymentWorkflow
{
    public record PaymentDetails(int Amount);

    [WorkflowRun]
    public async Task RunAsync(PaymentDetails details)
    {
        // Request authorization then mark as authorized so early return can complete
        Workflow.Logger.LogInformation("Requesting authorization for {Amount}", details.Amount);
        await Workflow.ExecuteActivityAsync(
            () => Activities.AuthorizePaymentAsync(details.Amount),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(10) });
        Authorized = true;

        // Now we can do the heavier payment processing/capture
        Workflow.Logger.LogInformation("Request authorized, requesting capture");
        await Workflow.ExecuteActivityAsync(
            () => Activities.CapturePaymentAsync(),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(10) });
        Workflow.Logger.LogInformation("Capture complete");
    }

    [WorkflowQuery]
    public bool Authorized { get; set; }

    [WorkflowUpdate]
    public Task WaitUntilAuthorizedAsync() => Workflow.WaitConditionAsync(() => Authorized);
}