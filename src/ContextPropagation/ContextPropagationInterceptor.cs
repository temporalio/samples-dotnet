namespace TemporalioSamples.ContextPropagation;

using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

/// <summary>
/// General purpose interceptor that can be used to propagate async-local context through workflows
/// and activities. This must be set on the client used for interacting with workflows and used for
/// the worker.
/// </summary>
/// <typeparam name="T">Context data type.</typeparam>
public class ContextPropagationInterceptor<T> : IClientInterceptor, IWorkerInterceptor
{
    private readonly AsyncLocal<T> context;
    private readonly IPayloadConverter payloadConverter;
    private readonly string headerKey;

    public ContextPropagationInterceptor(
        AsyncLocal<T> context,
        IPayloadConverter payloadConverter,
        string headerKey = "__my_context_key")
    {
        this.context = context;
        this.payloadConverter = payloadConverter;
        this.headerKey = headerKey;
    }

    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
        new ContextPropagationClientOutboundInterceptor(this, nextInterceptor);

    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new ContextPropagationWorkflowInboundInterceptor(this, nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ContextPropagationActivityInboundInterceptor(this, nextInterceptor);

    public NexusOperationInboundInterceptor InterceptNexusOperation(
        NexusOperationInboundInterceptor nextInterceptor) =>
        new ContextPropagationNexusOperationInboundInterceptor(this, nextInterceptor);

    private Dictionary<string, Payload> HeaderFromContext(IDictionary<string, Payload>? existing)
    {
        var ret = existing != null ?
            new Dictionary<string, Payload>(existing) : new Dictionary<string, Payload>(1);
        ret[headerKey] = payloadConverter.ToPayload(context.Value);
        return ret;
    }

    private void WithHeadersApplied(IReadOnlyDictionary<string, Payload>? headers, Action func) =>
        WithHeadersApplied(
            headers,
            () =>
            {
                func();
                return (object?)null;
            });

    private TResult WithHeadersApplied<TResult>(
        IReadOnlyDictionary<string, Payload>? headers, Func<TResult> func)
    {
        if (headers?.TryGetValue(headerKey, out var payload) == true && payload != null)
        {
            context.Value = payloadConverter.ToValue<T>(payload);
        }
        // These are async local, no need to unapply afterwards
        return func();
    }

    private Dictionary<string, string> HeaderFromContextForNexus(IDictionary<string, string>? existing)
    {
        var ret = existing != null ?
            new Dictionary<string, string>(existing) : new Dictionary<string, string>(1);
        // Nexus headers are string-based, so serialize context value to JSON.
        // Alternative approach: could use payload converter and put entire payload as JSON on header.
        ret[headerKey] = System.Text.Json.JsonSerializer.Serialize(context.Value);
        return ret;
    }

    private Task<TResult> WithHeadersAppliedForNexusAsync<TResult>(
        IReadOnlyDictionary<string, string>? headers, Func<Task<TResult>> func)
    {
        if (headers?.TryGetValue(headerKey, out var value) == true)
        {
            // Deserialize can return null for nullable types, which is expected
            context.Value = System.Text.Json.JsonSerializer.Deserialize<T>(value)!;
        }
        // These are async local, no need to unapply afterwards
        return func();
    }

    private class ContextPropagationClientOutboundInterceptor : ClientOutboundInterceptor
    {
        private readonly ContextPropagationInterceptor<T> root;

        public ContextPropagationClientOutboundInterceptor(
            ContextPropagationInterceptor<T> root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input) =>
            base.StartWorkflowAsync<TWorkflow, TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task SignalWorkflowAsync(SignalWorkflowInput input) =>
            base.SignalWorkflowAsync(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input) =>
            base.QueryWorkflowAsync<TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
            StartWorkflowUpdateInput input) =>
            base.StartWorkflowUpdateAsync<TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });
    }

    private class ContextPropagationWorkflowInboundInterceptor : WorkflowInboundInterceptor
    {
        private readonly ContextPropagationInterceptor<T> root;

        public ContextPropagationWorkflowInboundInterceptor(
            ContextPropagationInterceptor<T> root, WorkflowInboundInterceptor next)
            : base(next) => this.root = root;

        public override void Init(WorkflowOutboundInterceptor outbound) =>
            base.Init(new ContextPropagationWorkflowOutboundInterceptor(root, outbound));

        public override Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input) =>
            root.WithHeadersApplied(Workflow.Info.Headers, () => Next.ExecuteWorkflowAsync(input));

        public override Task HandleSignalAsync(HandleSignalInput input) =>
            root.WithHeadersApplied(input.Headers, () => Next.HandleSignalAsync(input));

        public override object? HandleQuery(HandleQueryInput input) =>
            root.WithHeadersApplied(input.Headers, () => Next.HandleQuery(input));

        public override void ValidateUpdate(HandleUpdateInput input) =>
            root.WithHeadersApplied(input.Headers, () => Next.ValidateUpdate(input));

        public override Task<object?> HandleUpdateAsync(HandleUpdateInput input) =>
            root.WithHeadersApplied(input.Headers, () => Next.HandleUpdateAsync(input));
    }

    private class ContextPropagationWorkflowOutboundInterceptor : WorkflowOutboundInterceptor
    {
        private readonly ContextPropagationInterceptor<T> root;

        public ContextPropagationWorkflowOutboundInterceptor(
            ContextPropagationInterceptor<T> root, WorkflowOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<TResult> ScheduleActivityAsync<TResult>(
            ScheduleActivityInput input) =>
            Next.ScheduleActivityAsync<TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task<TResult> ScheduleLocalActivityAsync<TResult>(
            ScheduleLocalActivityInput input) =>
            Next.ScheduleLocalActivityAsync<TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task SignalChildWorkflowAsync(
            SignalChildWorkflowInput input) =>
            Next.SignalChildWorkflowAsync(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task SignalExternalWorkflowAsync(
            SignalExternalWorkflowInput input) =>
            Next.SignalExternalWorkflowAsync(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            StartChildWorkflowInput input) =>
            Next.StartChildWorkflowAsync<TWorkflow, TResult>(
                input with { Headers = root.HeaderFromContext(input.Headers) });

        public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            StartNexusOperationInput input) =>
            Next.StartNexusOperationAsync<TResult>(
                input with { Headers = root.HeaderFromContextForNexus(input.Headers) });
    }

    private class ContextPropagationActivityInboundInterceptor : ActivityInboundInterceptor
    {
        private readonly ContextPropagationInterceptor<T> root;

        public ContextPropagationActivityInboundInterceptor(
            ContextPropagationInterceptor<T> root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input) =>
            root.WithHeadersApplied(input.Headers, () => Next.ExecuteActivityAsync(input));
    }

    private class ContextPropagationNexusOperationInboundInterceptor : NexusOperationInboundInterceptor
    {
        private readonly ContextPropagationInterceptor<T> root;

        public ContextPropagationNexusOperationInboundInterceptor(
            ContextPropagationInterceptor<T> root, NexusOperationInboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
            ExecuteNexusOperationStartInput input) =>
            root.WithHeadersAppliedForNexusAsync(
                input.Context.Headers,
                () => base.ExecuteNexusOperationStartAsync(input));
    }
}