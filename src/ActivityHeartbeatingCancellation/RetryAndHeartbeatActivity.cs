using Temporalio.Activities;

namespace RetryAndHeartbeat;

public class RetryAndHeartbeatActivity
{
    [Activity]
    public async Task<string> ProcessAsync(string input)
    {
        for (int i = 0; i < 5; i++)
        {
            // Send progress
            ActivityExecutionContext.Current.Heartbeat(i);
            await Task.Delay(500);
        }

        return $"Processed: {input}";
    }
}
