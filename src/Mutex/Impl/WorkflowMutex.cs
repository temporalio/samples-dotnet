namespace TemporalioSamples.Mutex.Impl;

using Temporalio.Workflows;

internal record AcquireLockInput(string ReleaseSignalName);

/// <summary>
/// Represents a mutual exclusion mechanism for Workflows.
/// This part contains API for acquiring locks.
/// </summary>
public static class WorkflowMutex
{
    private const string MutexWorkflowIdPrefix = "__wm-lock:";

    public static async Task<ILockHandle> LockAsync(string resourceId, TimeSpan? lockTimeout = null)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException("Cannot acquire a lock outside of a workflow.");
        }

        var initiatorId = Workflow.Info.WorkflowId;
        var lockStarted = Workflow.UtcNow;

        string? releaseSignalName = null;
        var acquireLockSignalName = Workflow.NewGuid().ToString();
        var signalDefinition = WorkflowSignalDefinition.CreateWithoutAttribute(acquireLockSignalName, (AcquireLockInput input) =>
        {
            releaseSignalName = input.ReleaseSignalName;

            return Task.CompletedTask;
        });
        Workflow.Signals[acquireLockSignalName] = signalDefinition;
        try
        {
            var startMutexWorkflowInput = new SignalWithStartMutexWorkflowInput($"{MutexWorkflowIdPrefix}{resourceId}", resourceId, acquireLockSignalName, lockTimeout);
            await Workflow.ExecuteActivityAsync<MutexActivities>(
                act => act.SignalWithStartMutexWorkflowAsync(startMutexWorkflowInput),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1), });

            await Workflow.WaitConditionAsync(() => releaseSignalName != null);

            var elapsed = Workflow.UtcNow - lockStarted;
            Workflow.Logger.LogInformation(
                "Lock for resource '{ResourceId}' acquired in {AcquireTime}ms by '{LockInitiatorId}', release signal name '{ReleaseSignalName}'",
                resourceId,
                (int)elapsed.TotalMilliseconds,
                initiatorId,
                releaseSignalName);

            return new LockHandle(initiatorId, startMutexWorkflowInput.MutexWorkflowId, resourceId, releaseSignalName!);
        }
        finally
        {
            Workflow.Signals.Remove(acquireLockSignalName);
        }
    }

    internal static ILockHandler CreateLockHandler()
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException("Cannot acquire a lock outside of a workflow.");
        }

        return new LockHandler();
    }

    internal sealed class LockHandle : ILockHandle
    {
        private readonly string mutexWorkflowId;

        public LockHandle(string lockInitiatorId, string mutexWorkflowId, string resourceId, string releaseSignalId)
        {
            LockInitiatorId = lockInitiatorId;
            this.mutexWorkflowId = mutexWorkflowId;
            ResourceId = resourceId;
            ReleaseSignalName = releaseSignalId;
        }

        /// <inheritdoc />
        public string LockInitiatorId { get; }

        /// <inheritdoc />
        public string ResourceId { get; }

        /// <inheritdoc />
        public string ReleaseSignalName { get; }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            var mutexHandle = Workflow.GetExternalWorkflowHandle(mutexWorkflowId);
            await mutexHandle.SignalAsync(ReleaseSignalName, Array.Empty<object?>());
        }
    }

    internal sealed class LockHandler : ILockHandler
    {
        /// <inheritdoc />
        public string? CurrentOwnerId { get; private set; }

        /// <inheritdoc />
        public async Task HandleAsync(LockRequest lockRequest)
        {
            var releaseSignalName = Workflow.NewGuid().ToString();

            var initiator = Workflow.GetExternalWorkflowHandle(lockRequest.InitiatorId);
            await initiator.SignalAsync(lockRequest.AcquireLockSignalName, new[] { new AcquireLockInput(releaseSignalName) });

            var released = false;
            Workflow.Signals[releaseSignalName] = WorkflowSignalDefinition.CreateWithoutAttribute(releaseSignalName, () =>
            {
                released = true;

                return Task.CompletedTask;
            });
            CurrentOwnerId = lockRequest.InitiatorId;

            if (!await Workflow.WaitConditionAsync(() => released, lockRequest.Timeout ?? Timeout.InfiniteTimeSpan))
            {
                Workflow.Logger.LogWarning(
                    "Lock for resource '{ResourceId}' has been timed out after '{Timeout}'. (LockInitiatorId='{LockInitiatorId}')",
                    Workflow.Info.WorkflowId[MutexWorkflowIdPrefix.Length..],
                    lockRequest.Timeout,
                    lockRequest.InitiatorId);
            }

            CurrentOwnerId = null;
            Workflow.Signals.Remove(releaseSignalName);
        }
    }
}
