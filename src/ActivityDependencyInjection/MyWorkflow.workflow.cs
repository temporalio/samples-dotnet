namespace TemporalioSamples.ActivityDependencyInjection;

using Temporalio.Workflows;

/// <summary>
/// Demonstration workflow.
/// </summary>
[Workflow]
public class MyWorkflow
{
    /// <summary>
    /// Task queue for workflow and all activities.
    /// </summary>
    public const string TaskQueue = "activity-di-sample";

    /// <summary>
    /// Workflow entry point.
    /// </summary>
    /// <returns>Task for completion.</returns>
    [WorkflowRun]
    public async Task RunAsync()
    {
        // Run the singleton one and the per-method one
        await Workflow.ExecuteActivityAsync(
            (MyActivitiesSingleton act) => act.DoSingletonDatabaseStuffAsync(),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        await Workflow.ExecuteActivityAsync(
            (MyActivitiesTransient act) => act.DoTransientDatabaseStuffAsync(),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
    }
}