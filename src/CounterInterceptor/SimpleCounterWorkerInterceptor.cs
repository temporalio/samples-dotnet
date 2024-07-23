namespace TermporalioSamples.CounterInterceptor;

using Temporalio.Activities;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

public class SimpleCounterWorkerInterceptor : IWorkerInterceptor
{
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(this, nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(this, nextInterceptor);

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
            WorkerCounter.Add(Workflow.Info.WorkflowId, WorkerCounter.NumberOfWorkflowExecutions);
            return base.ExecuteWorkflowAsync(input);
        }

        public override Task HandleSignalAsync(HandleSignalInput input)
        {
            WorkerCounter.Add(Workflow.Info.WorkflowId, WorkerCounter.NumberOfSignals);
            return base.HandleSignalAsync(input);
        }

        public override object? HandleQuery(HandleQueryInput input)
        {
            WorkerCounter.Add(Workflow.Info.WorkflowId, WorkerCounter.NumberOfQueries);
            return base.HandleQuery(input);
        }

        public override void ValidateUpdate(HandleUpdateInput input)
        {
            // not monitoring
            base.ValidateUpdate(input);
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
            WorkerCounter.Add(Workflow.Info.WorkflowId, WorkerCounter.NumberOfChildWorkflowExecutions);
            return base.StartChildWorkflowAsync<TWorkflow, TResult>(input);
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly SimpleCounterWorkerInterceptor root;

        internal ActivityInbound(SimpleCounterWorkerInterceptor root, ActivityInboundInterceptor next)
            : base(next)
        {
            this.root = root;
        }

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            WorkerCounter.Add(ActivityExecutionContext.Current.Info.WorkflowId, WorkerCounter.NumberOfActivityExecutions);
            return base.ExecuteActivityAsync(input);
        }
    }
}