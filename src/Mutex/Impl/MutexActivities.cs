namespace TemporalioSamples.Mutex.Impl;

using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Workflows;

internal record SignalWithStartMutexWorkflowInput(string MutexWorkflowId, string ResourceId, string AcquireLockSignalName, TimeSpan? LockTimeout = null);

internal class MutexActivities
{
    private static readonly string RequestLockSignalName =
        WorkflowSignalDefinition.FromMethod(
                typeof(MutexWorkflow).GetMethod(nameof(MutexWorkflow.RequestLockAsync))
                ?? throw new InvalidOperationException($"Method {nameof(MutexWorkflow.RequestLockAsync)} not found on type {typeof(MutexWorkflow)}"))
            .Name ?? throw new InvalidOperationException("Signal name is null.");

    private readonly ITemporalClient client;

    public MutexActivities(ITemporalClient client)
    {
        this.client = client;
    }

    [Activity]
    public async Task SignalWithStartMutexWorkflowAsync(SignalWithStartMutexWorkflowInput input)
    {
        var activityInfo = ActivityExecutionContext.Current.Info;
        var workflowId = activityInfo.IsWorkflowActivity ? activityInfo.WorkflowId! : throw new InvalidOperationException("Activity must be invoked from a workflow");

        await this.client.StartWorkflowAsync(
            (MutexWorkflow mw) => mw.RunAsync(MutexWorkflowInput.Empty),
            new WorkflowOptions(input.MutexWorkflowId, activityInfo.TaskQueue)
            {
                StartSignal = RequestLockSignalName,
                StartSignalArgs = new object[] { new LockRequest(workflowId, input.AcquireLockSignalName, input.LockTimeout), },
            });
    }
}
