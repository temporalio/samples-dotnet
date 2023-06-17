using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace TemporalioSamples.ActivityHeartbeatingCancellation;

public static class MyActivities
{
    [Activity]
    public static async Task FakeProgressAsync(int sleepIntervalMs = 1000)
    {
        try
        {
            // Allow for resuming from heartbeat
            var startingPoint = ActivityExecutionContext.Current.Info.HeartbeatDetails.Any()
                ? await ActivityExecutionContext.Current.Info.HeartbeatDetailAtAsync<int>(0)
                : 1;

            ActivityExecutionContext.Current.Logger.LogInformation("Starting activity at progress: {StartingPoint}", startingPoint);

            for (var progress = startingPoint; progress <= 100; ++progress)
            {
                ActivityExecutionContext.Current.CancellationToken.ThrowIfCancellationRequested();
                ActivityExecutionContext.Current.Logger.LogInformation("Progress: {Progress}", progress);
                await Task.Delay(sleepIntervalMs);
                ActivityExecutionContext.Current.Heartbeat(progress);
            }

            ActivityExecutionContext.Current.Logger.LogInformation("Fake progress activity completed");
        }
        catch (OperationCanceledException e) when (e.InnerException is CancelledFailureException)
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Fake progress activity cancelled");
            throw;
        }
    }
}