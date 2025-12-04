using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.UpdatableTimer;

public class UpdatableTimer(DateTimeOffset wakeUpTime)
{
    private bool wakeUpTimeUpdated;
    private DateTimeOffset wakeUpTime = wakeUpTime;

    public async Task SleepAsync()
    {
        Workflow.Logger.LogInformation("Sleep until: {WakeUpTime}", wakeUpTime);

        while (true)
        {
            var sleepInterval = wakeUpTime - Workflow.UtcNow;
            if (sleepInterval <= TimeSpan.Zero)
            {
                break;
            }

            Workflow.Logger.LogInformation("Going to sleep for {SleepInterval}", sleepInterval);

            wakeUpTimeUpdated = false;
            if (!await Workflow.WaitConditionWithOptionsAsync(new(() => wakeUpTimeUpdated, sleepInterval, $"Going to sleep for {sleepInterval}")))
            {
                break;
            }
        }

        Workflow.Logger.LogInformation("Sleep completed");
    }

    public void UpdateWakeUpTime(DateTimeOffset newWakeUpTime)
    {
        wakeUpTime = newWakeUpTime;
        wakeUpTimeUpdated = true;
    }

    public DateTimeOffset WakeUpTime => wakeUpTime;
}