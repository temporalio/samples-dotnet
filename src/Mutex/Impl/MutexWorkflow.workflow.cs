namespace TemporalioSamples.Mutex.Impl;

using Temporalio.Workflows;

internal record MutexWorkflowInput(IReadOnlyCollection<LockRequest> InitialRequests)
{
    public static readonly MutexWorkflowInput Empty = new(Array.Empty<LockRequest>());
}

[Workflow]
internal class MutexWorkflow
{
    private readonly ILockHandler lockHandler = WorkflowMutex.CreateLockHandler();
    private readonly Queue<LockRequest> requests = new();

    [WorkflowRun]
    public async Task RunAsync(MutexWorkflowInput input)
    {
        var logger = Workflow.Logger;

        foreach (var request in input.InitialRequests)
        {
            requests.Enqueue(request);
        }

        while (!Workflow.ContinueAsNewSuggested)
        {
            if (requests.Count == 0)
            {
                logger.LogInformation("No lock requests, waiting for more...");

                await Workflow.WaitConditionAsync(() => requests.Count > 0);
            }

            while (requests.TryDequeue(out var lockRequest))
            {
                await lockHandler.HandleAsync(lockRequest);
            }
        }

        if (requests.Count > 0)
        {
            var newInput = new MutexWorkflowInput(requests);
            throw Workflow.CreateContinueAsNewException((MutexWorkflow x) => x.RunAsync(newInput));
        }
    }

    [WorkflowQuery]
    public string? CurrentOwnerId => lockHandler.CurrentOwnerId;

    [WorkflowSignal]
    public Task RequestLockAsync(LockRequest request)
    {
        requests.Enqueue(request);

        Workflow.Logger.LogInformation("Received lock request. (InitiatorId='{InitiatorId}')", request.InitiatorId);

        return Task.CompletedTask;
    }
}
