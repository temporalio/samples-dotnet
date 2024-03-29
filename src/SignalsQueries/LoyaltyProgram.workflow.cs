namespace TemporalioSamples.SignalsQueries;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class LoyaltyProgram
{
    private string? userId;

    [WorkflowQuery]
    public int Points { get; private set; }

    [WorkflowRun]
    public async Task RunAsync(string userId)
    {
        this.userId = userId;

        // Keep this workflow running forever
        await Workflow.WaitConditionAsync(() => false);
    }

    [WorkflowSignal]
    public async Task NotifyPurchaseAsync(int purchaseTotalCents)
    {
        Points += purchaseTotalCents;
        Workflow.Logger.LogInformation("Added {Result} points, total: {Total}", purchaseTotalCents, Points);

        if (Points >= 10_000)
        {
            await Workflow.ExecuteActivityAsync(
                () => MyActivities.SendCoupon(userId),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            Points -= 10_000;
        }
    }
}