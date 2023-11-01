namespace TemporalioSamples.Mutex;

using Temporalio.Workflows;
using TemporalioSamples.Mutex.Impl;

public record WorkflowWithMutexInput(string ResourceId, TimeSpan SleepFor, TimeSpan LockTimeout);

[Workflow]
public class WorkflowWithMutex
{
    private static readonly ActivityOptions ActivityOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(1),
    };

    [WorkflowRun]
    public async Task RunAsync(WorkflowWithMutexInput input)
    {
        var currentWorkflowId = Workflow.Info.WorkflowId;
        var logger = Workflow.Logger;

        using var ls1 = logger.BeginScope(new KeyValuePair<string, object?>[]
        {
            new("ResourceId", input.ResourceId),
        });

        logger.LogInformation("Started workflow '{WorkflowId}'!", currentWorkflowId);

        await using (var lockHandle = await WorkflowMutex.LockAsync(input.ResourceId, input.LockTimeout))
        {
            logger.LogInformation("[{WorkflowId}]: Acquired lock. Release signal ID: '{ReleaseSignalId}'", currentWorkflowId, lockHandle.ReleaseSignalName);

            var notifyLockedInput = new NotifyLockedInput(input.ResourceId, lockHandle.ReleaseSignalName);
            await Workflow.ExecuteActivityAsync(() => Activities.NotifyLocked(notifyLockedInput), ActivityOptions);

            var useApiInput = new UseApiThatCantBeCalledInParallelInput(input.SleepFor);
            await Workflow.ExecuteActivityAsync(() => Activities.UseApiThatCantBeCalledInParallelAsync(useApiInput), ActivityOptions);
        }

        var notifyUnlockedInput = new NotifyUnlockedInput(input.ResourceId);
        await Workflow.ExecuteActivityAsync(() => Activities.NotifyUnlocked(notifyUnlockedInput), ActivityOptions);
    }
}
