namespace TemporalioSamples.SignalsQueries;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class LoyaltyProgram
{
    private string? userId;
    private int points = 0;

    [WorkflowRun]
    public async Task RunAsync(string userId)
    {
        this.userId = userId;

        // TODO dear chad, how do i await cancellation? or something short/simple that prevents from returning
    }

    [WorkflowSignal]
    public async Task NotifyPurchaseAsync(int purchaseTotalCents)
    {
        points += purchaseTotalCents;
        Workflow.Logger.LogInformation("Added {Result} points, total: {Total}", purchaseTotalCents, points);

        if (points >= 10_000)
        {
            Workflow.Logger.LogInformation("Sending coupon to {UserId}", userId);
            await Workflow.ExecuteActivityAsync(
                () => MyActivities.SendCoupon(userId),
                new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
            points -= 10_000;
        }
    }

    [WorkflowQuery]
    public int GetPoints() => points;
}