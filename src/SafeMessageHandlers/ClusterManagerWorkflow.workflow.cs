namespace TemporalioSamples.SafeMessageHandlers;

using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class ClusterManagerWorkflow
{
    public record State
    {
        public bool ClusterStarted { get; set; }

        public bool ClusterShutdown { get; set; }

        public IDictionary<string, string?> Nodes { get; init; } = new Dictionary<string, string?>();

        public int MaxAssignedNodes { get; set; }
    }

    public record Input
    {
        public State State { get; init; } = new();

        public bool TestContinueAsNew { get; init; }
    }

    public record Result(
        int MaxAssignedNodes,
        int NumAssignedNodes);

    private readonly Semaphore nodesLock = new(1);
    private readonly int maxHistoryLength;
    private readonly TimeSpan sleepInterval;

    [WorkflowInit]
    public ClusterManagerWorkflow(Input input)
    {
        CurrentState = input.State;
        maxHistoryLength = input.TestContinueAsNew ? 40 : int.MaxValue;
        sleepInterval = TimeSpan.FromSeconds(input.TestContinueAsNew ? 1 : 600);
    }

    [WorkflowQuery]
    public State CurrentState { get; init; }

    [WorkflowRun]
    public async Task<Result> RunAsync(Input input)
    {
        await Workflow.WaitConditionAsync(() => CurrentState.ClusterStarted);

        // Perform health checks at intervals
        do
        {
            await PerformHealthChecksAsync();
            await Workflow.WaitConditionAsync(
                () => CurrentState.ClusterShutdown || ShouldContinueAsNew,
                sleepInterval);

            // Continue as new if needed
            if (ShouldContinueAsNew)
            {
                Workflow.Logger.LogInformation("Continuing as new");
                throw Workflow.CreateContinueAsNewException((ClusterManagerWorkflow wf) => wf.RunAsync(new()
                {
                    State = CurrentState,
                    TestContinueAsNew = input.TestContinueAsNew,
                }));
            }
        }
        while (!CurrentState.ClusterShutdown);
        return new(CurrentState.MaxAssignedNodes, NumAssignedNodes);
    }

    [WorkflowSignal]
    public async Task StartClusterAsync()
    {
        CurrentState.ClusterStarted = true;
        foreach (var node in Enumerable.Range(0, 25))
        {
            CurrentState.Nodes[$"{node}"] = null;
        }
        Workflow.Logger.LogInformation("Cluster started");
    }

    [WorkflowSignal]
    public async Task ShutdownClusterAsync()
    {
        await Workflow.WaitConditionAsync(() => CurrentState.ClusterStarted);
        CurrentState.ClusterShutdown = true;
        Workflow.Logger.LogInformation("Cluster shut down");
    }

    public record AllocateNodesToJobInput(int NumNodes, string JobName);

    [WorkflowUpdate]
    public async Task<List<string>> AllocateNodesToJobAsync(AllocateNodesToJobInput input)
    {
        await Workflow.WaitConditionAsync(() => CurrentState.ClusterStarted);
        if (CurrentState.ClusterShutdown)
        {
            throw new ApplicationFailureException(
                "Cannot allocate nodes to a job, cluster is already shut down");
        }
        await nodesLock.WaitAsync();
        try
        {
            var unassignedNodes = CurrentState.Nodes.
                Where(kvp => kvp.Value == null).
                Select(kvp => kvp.Key).
                ToList();
            if (unassignedNodes.Count < input.NumNodes)
            {
                throw new ApplicationFailureException(
                    $"Cannot allocate {input.NumNodes} nodes, have only {unassignedNodes.Count} available");
            }
            var assignedNodes = unassignedNodes[..input.NumNodes];
            // This await would be dangerous without nodesLock because it yields control and allows
            // interleaving
            await Workflow.ExecuteActivityAsync(
                (ClusterManagerActivities acts) => acts.AllocateNodesToJobAsync(new(assignedNodes, input.JobName)),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            foreach (var node in assignedNodes)
            {
                CurrentState.Nodes[node] = input.JobName;
            }
            CurrentState.MaxAssignedNodes = int.Max(CurrentState.MaxAssignedNodes, NumAssignedNodes);
            return assignedNodes;
        }
        finally
        {
            nodesLock.Release();
        }
    }

    public record DeleteJobInput(string JobName);

    [WorkflowUpdate]
    public async Task DeleteJobAsync(DeleteJobInput input)
    {
        await Workflow.WaitConditionAsync(() => CurrentState.ClusterStarted);
        if (CurrentState.ClusterShutdown)
        {
            throw new ApplicationFailureException(
                "Cannot delete job, cluster is already shut down");
        }
        await nodesLock.WaitAsync();
        try
        {
            var toUnassign = CurrentState.Nodes.
                Where(kvp => kvp.Value == input.JobName).
                Select(kvp => kvp.Key).
                ToList();
            // This await would be dangerous without nodesLock because it yields control and allows
            // interleaving
            await Workflow.ExecuteActivityAsync(
                (ClusterManagerActivities acts) => acts.DeallocateNodesFromJobAsync(new(toUnassign, input.JobName)),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
            foreach (var node in toUnassign)
            {
                CurrentState.Nodes[node] = null;
            }
        }
        finally
        {
            nodesLock.Release();
        }
    }

    private int NumAssignedNodes =>
        CurrentState.Nodes.Count(kvp => kvp.Value is { } val && val != "BAD!");

    private bool ShouldContinueAsNew =>
        // Don't continue as new while update running
        Workflow.AllHandlersFinished &&
        // Continue if suggested or, for ease of testing, max history reached
        (Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > maxHistoryLength);

    private async Task PerformHealthChecksAsync()
    {
        await nodesLock.WaitAsync();
        try
        {
            // Find bad nodes from the set of non-bad ones. This await would be dangerous without
            // nodesLock because it yields control and allows interleaving.
            var assignedNodes = CurrentState.Nodes.
                Where(kvp => kvp.Value is { } val && val != "BAD!").
                Select(kvp => kvp.Value!).
                ToList();
            var badNodes = await Workflow.ExecuteActivityAsync(
                (ClusterManagerActivities acts) => acts.FindBadNodesAsync(new(assignedNodes)),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    // This health check is optional, and our lock would block the whole workflow if
                    // we let it retry forever
                    RetryPolicy = new() { MaximumAttempts = 1 },
                });
            foreach (var node in badNodes)
            {
                CurrentState.Nodes[node] = "BAD!";
            }
        }
        finally
        {
            nodesLock.Release();
        }
    }
}