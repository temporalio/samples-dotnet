using Temporalio.Workflows;

namespace TemporalioSamples.Schedules;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string text)
    {
        await Workflow.ExecuteActivityAsync(
            () => MyActivities.AddReminderToDatabase(text),
            new()
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
            });

        await Workflow.ExecuteActivityAsync(
            () => MyActivities.NotifyUserAsync(text),
            new()
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
            });
    }
}