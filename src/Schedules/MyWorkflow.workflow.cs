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
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });

        await Workflow.ExecuteActivityAsync(
            () => MyActivities.NotifyUserAsync(text),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });
    }
}