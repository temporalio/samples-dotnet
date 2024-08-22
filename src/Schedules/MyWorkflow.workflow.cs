using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Workflows;

namespace TemporalioSamples.Schedules;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string text)
    {
        Workflow.Logger.LogInformation("Schedule workflow started. {StartTime}", Workflow.UtcNow);

        var scheduledById = Workflow.TypedSearchAttributes.Get(SearchAttributeKey.CreateKeyword("TemporalScheduledById"));
        var startTime = Workflow.TypedSearchAttributes.Get(SearchAttributeKey.CreateDateTimeOffset("TemporalScheduledStartTime"));

        Workflow.Logger.LogInformation("Scheduled by id: {ScheduledById} Start time: {StartTime}", scheduledById, startTime);

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