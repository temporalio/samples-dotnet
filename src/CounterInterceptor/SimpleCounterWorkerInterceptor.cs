namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Activities;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

public record WorkflowCounts
{
    public uint Executions { get; internal set; }

    public uint Signals { get; internal set; }

    public uint Queries { get; internal set; }

    public uint ChildExecutions { get; internal set; }

    public uint ActivityExecutions { get; internal set; }

    public override string ToString() =>
        $"\n\tTotal Number of Workflow Exec: {Executions}\n\t" +
        $"Total Number of Child Workflow Exec: {ChildExecutions}\n\t" +
        $"Total Number of Activity Exec: {ActivityExecutions}\n\t" +
        $"Total Number of Signals: {Signals}\n\t" +
        $"Total Number of Queries: {Queries}";
}

public class SimpleCounterWorkerInterceptor : IWorkerInterceptor
{
    private const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    private const string NumberOfChildWorkflowExecutions = "numOfChildWorkflowExec";
    private const string NumberOfActivityExecutions = "numOfActivityExec";
    private const string NumberOfSignals = "numOfSignals";
    private const string NumberOfQueries = "numOfQueries";

    private Dictionary<string, WorkflowCounts> counterDictionary = new Dictionary<string, WorkflowCounts>();

    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(this, nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(this, nextInterceptor);

    public string Info() =>
        string.Join(
                "\n",
                counterDictionary.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value}"));

    public uint NumOfWorkflowExecutions(string workflowId) =>
        counterDictionary[workflowId].Executions;

    public uint NumOfChildWorkflowExecutions(string workflowId) =>
        counterDictionary[workflowId].ChildExecutions;

    public uint NumOfActivityExecutions(string workflowId) =>
        counterDictionary[workflowId].ActivityExecutions;

    public uint NumOfSignals(string workflowId) =>
        counterDictionary[workflowId].Signals;

    public uint NumOfQueries(string workflowId) =>
        counterDictionary[workflowId].Queries;

    private void Add(string workflowId, string type)
    {
        if (!counterDictionary.TryGetValue(workflowId, out WorkflowCounts? value))
        {
            value = new WorkflowCounts();
            counterDictionary.Add(workflowId, value);
        }

        switch (type)
        {
            case NumberOfActivityExecutions:
                value.ActivityExecutions++;
                break;
            case NumberOfChildWorkflowExecutions:
                value.ChildExecutions++;
                break;
            case NumberOfQueries:
                value.Queries++;
                break;
            case NumberOfSignals:
                value.Signals++;
                break;
            case NumberOfWorkflowExecutions:
                value.Executions++;
                break;
            default:
                throw new NotImplementedException($"Unknown type: " + type);
        }
    }

    private class WorkflowInbound : WorkflowInboundInterceptor
    {
        private readonly SimpleCounterWorkerInterceptor root;

        internal WorkflowInbound(SimpleCounterWorkerInterceptor root, WorkflowInboundInterceptor next)
            : base(next) => this.root = root;

        public override void Init(WorkflowOutboundInterceptor outbound)
        {
            base.Init(new WorkflowOutbound(root, outbound));
        }

        public override Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            root.Add(Workflow.Info.WorkflowId, NumberOfWorkflowExecutions);
            return base.ExecuteWorkflowAsync(input);
        }

        public override Task HandleSignalAsync(HandleSignalInput input)
        {
            root.Add(Workflow.Info.WorkflowId, NumberOfSignals);
            return base.HandleSignalAsync(input);
        }

        public override object? HandleQuery(HandleQueryInput input)
        {
            root.Add(Workflow.Info.WorkflowId, NumberOfQueries);
            return base.HandleQuery(input);
        }
    }

    private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
    {
        private readonly SimpleCounterWorkerInterceptor root;

        internal WorkflowOutbound(SimpleCounterWorkerInterceptor root, WorkflowOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<TResult> ScheduleActivityAsync<TResult>(
            ScheduleActivityInput input)
        {
            return base.ScheduleActivityAsync<TResult>(input);
        }

        public override Task SignalChildWorkflowAsync(SignalChildWorkflowInput input)
        {
            return base.SignalChildWorkflowAsync(input);
        }

        public override Task SignalExternalWorkflowAsync(SignalExternalWorkflowInput input)
        {
            return base.SignalExternalWorkflowAsync(input);
        }

        public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            StartChildWorkflowInput input)
        {
            root.Add(Workflow.Info.WorkflowId, NumberOfChildWorkflowExecutions);
            return base.StartChildWorkflowAsync<TWorkflow, TResult>(input);
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly SimpleCounterWorkerInterceptor root;

        internal ActivityInbound(SimpleCounterWorkerInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            root.Add(ActivityExecutionContext.Current.Info.WorkflowId, NumberOfActivityExecutions);
            return base.ExecuteActivityAsync(input);
        }
    }
}