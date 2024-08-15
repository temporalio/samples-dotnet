namespace TemporalioSamples.CounterInterceptor;

using System.Collections.Concurrent;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

public class MyCounterInterceptor : IClientInterceptor, IWorkerInterceptor
{
    private ConcurrentDictionary<string, Counts> counts = new();

    public ConcurrentDictionary<string, Counts> Counts => counts;

    public string WorkerInfo() =>
        string.Join(
                "\n",
                counts.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value.WorkflowInfo()}"));

    public string ClientInfo() =>
        string.Join(
                "\n",
                counts.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value.ClientInfo()}"));

    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
        new ClientOutbound(this, nextInterceptor);

    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(this, nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(this, nextInterceptor);

    private sealed class ClientOutbound : ClientOutboundInterceptor
    {
        private MyCounterInterceptor root;

        public ClientOutbound(MyCounterInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            var id = input.Options.Id ?? "None";
            // Need to add an empty record if none exists
            // we don't care if it doesn't add it as we will
            // still increment the current value.
            root.counts.TryAdd(id, new Counts());
            Interlocked.Increment(ref root.counts[id].ClientExecutions);
            return base.StartWorkflowAsync<TWorkflow, TResult>(input);
        }

        public override Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            var id = input.Id ?? "None";
            Interlocked.Increment(ref root.counts[id].ClientSignals);
            return base.SignalWorkflowAsync(input);
        }

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            var id = input.Id ?? "None";
            Interlocked.Increment(ref root.counts[id].ClientQueries);
            return base.QueryWorkflowAsync<TResult>(input);
        }
    }

    private sealed class WorkflowInbound : WorkflowInboundInterceptor
    {
        private readonly MyCounterInterceptor root;

        internal WorkflowInbound(MyCounterInterceptor root, WorkflowInboundInterceptor next)
            : base(next) => this.root = root;

        public override void Init(WorkflowOutboundInterceptor outbound)
        {
            base.Init(new WorkflowOutbound(root, outbound));
        }

        public override Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            var id = Workflow.Info.WorkflowId;
            // Need to add an empty record if none exists
            // we don't care if it doesn't add it as we will
            // still increment the current value.
            root.counts.TryAdd(id, new Counts());
            Interlocked.Increment(ref root.counts[id].WorkflowExecutions);
            return base.ExecuteWorkflowAsync(input);
        }

        public override Task HandleSignalAsync(HandleSignalInput input)
        {
            var id = Workflow.Info.WorkflowId;
            Interlocked.Increment(ref root.counts[id].WorkflowSignals);
            return base.HandleSignalAsync(input);
        }

        public override object? HandleQuery(HandleQueryInput input)
        {
            var id = Workflow.Info.WorkflowId;
            Interlocked.Increment(ref root.counts[id].WorkflowQueries);
            return base.HandleQuery(input);
        }
    }

    private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
    {
        private readonly MyCounterInterceptor root;

        internal WorkflowOutbound(MyCounterInterceptor root, WorkflowOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            StartChildWorkflowInput input)
        {
            var id = Workflow.Info.WorkflowId;
            Interlocked.Increment(ref root.counts[id].WorkflowChildExecutions);
            return base.StartChildWorkflowAsync<TWorkflow, TResult>(input);
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly MyCounterInterceptor root;

        internal ActivityInbound(MyCounterInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            var id = ActivityExecutionContext.Current.Info.WorkflowId;
            Interlocked.Increment(ref root.counts[id].WorkflowActivityExecutions);
            return base.ExecuteActivityAsync(input);
        }
    }
}