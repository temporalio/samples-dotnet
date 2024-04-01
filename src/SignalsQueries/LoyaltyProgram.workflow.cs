namespace TemporalioSamples.SignalsQueries;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

public record Purchase(string Id, int TotalCents);

[Workflow]
public class LoyaltyProgram
{
    private readonly Queue<Purchase> toProcess = new();

    [WorkflowQuery]
    public int Points { get; private set; }

    [WorkflowRun]
    public async Task RunAsync(string userId)
    {
        while (true)
        {
            // Wait for purchase
            await Workflow.WaitConditionAsync(() => toProcess.Count > 0);

            // Process
            var purchase = toProcess.Dequeue();
            Points += purchase.TotalCents;
            Workflow.Logger.LogInformation("Added {TotalCents} points, total: {Points}", purchase.TotalCents, Points);
            if (Points >= 10_000)
            {
                await Workflow.ExecuteActivityAsync(
                    () => MyActivities.SendCoupon(userId),
                    new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
                Points -= 10_000;
                Workflow.Logger.LogInformation("Remaining points: {Points}", Points);
            }
        }
    }

    [WorkflowSignal]
    public async Task NotifyPurchaseAsync(Purchase purchase)
    {
        if (!toProcess.Contains(purchase))
        {
            toProcess.Enqueue(purchase);
        }
    }
}