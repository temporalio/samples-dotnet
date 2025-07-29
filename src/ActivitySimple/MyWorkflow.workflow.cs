using Temporalio.Exceptions;

namespace TemporalioSamples.ActivitySimple;

using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(bool fail = false)
    {
        try
        {
            var table = "some-db-table";
            if (fail)
            {
                table = "fail hard";
            }
            // Run an async instance method activity.
            var result1 = await Workflow.ExecuteActivityAsync(
                (MyActivities act) => act.SelectFromDatabaseAsync(table),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Workflow.Logger.LogInformation("Activity instance method result: {Result}", result1);
        }
        catch (ActivityFailureException e)
        {
            // This will catch the Activity failure, but remember that you raised an `Applicationfailure` on the
            // inside so you need to "dig" into that to get the underlying ErrorType (string)
            Console.WriteLine($"ActivityFailure caught: {e.Message} with type {e.Failure.Cause.ApplicationFailureInfo.Type}");
            Workflow.Logger.LogError(e, "Activity failed");
        }

        // Run a sync static method activity.
        var result2 = await Workflow.ExecuteActivityAsync(
            () => MyActivities.DoStaticThing(),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
            });
        Workflow.Logger.LogInformation("Activity static method result: {Result}", result2);

        // We'll go ahead and return this result
        return result2;
    }
}