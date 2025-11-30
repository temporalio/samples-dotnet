using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace TemporalioSamples.UpdatableTimer;

[Workflow]
public class MyWorkflow
{
    private readonly UpdatableTimer timer;

    [WorkflowInit]
    public MyWorkflow(DateTimeOffset wakeUpTime) => timer = new UpdatableTimer(wakeUpTime);

    [WorkflowRun]
    public async Task RunAsync(DateTimeOffset wakeUpTime) => await timer.SleepAsync();

    [WorkflowSignal]
    public async Task UpdateWakeUpAsync(DateTimeOffset wakeUpTime)
    {
        Workflow.Logger.LogInformation("Update wake up time: {WakeUpTime}", wakeUpTime);
        timer.UpdateWakeUpTime(wakeUpTime);
    }

    [WorkflowQuery]
    public DateTimeOffset GetWakeUpTime => timer.WakeUpTime;
}