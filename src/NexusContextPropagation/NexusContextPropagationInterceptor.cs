namespace TemporalioSamples.NexusContextPropagation;

using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

public class NexusContextPropagationInterceptor(
    AsyncLocal<string?> context,
    string headerKey = "__my_context_key") : IClientInterceptor, IWorkerInterceptor
{
    public NexusOperationInboundInterceptor InterceptNexusOperation(
        NexusOperationInboundInterceptor nextInterceptor) =>
        new NexusOperationInbound(context, headerKey, nextInterceptor);

    public WorkflowInboundInterceptor InterceptWorkflow(
        WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(context, headerKey, nextInterceptor);

    private class NexusOperationInbound(
        AsyncLocal<string?> context,
        string headerKey,
        NexusOperationInboundInterceptor next) : NexusOperationInboundInterceptor(next)
    {
        public override Task<OperationStartResult<object?>> ExecuteNexusOperationStartAsync(
            ExecuteNexusOperationStartInput input)
        {
            if (input.Context.Headers?.TryGetValue(headerKey, out var value) == true)
            {
                context.Value = value;
            }
            return base.ExecuteNexusOperationStartAsync(input);
        }
    }

    private class WorkflowInbound(
        AsyncLocal<string?> context,
        string headerKey,
        WorkflowInboundInterceptor next) : WorkflowInboundInterceptor(next)
    {
        public override void Init(WorkflowOutboundInterceptor outbound) =>
            base.Init(new WorkflowOutbound(context, headerKey, outbound));
    }

    private class WorkflowOutbound(
        AsyncLocal<string?> context,
        string headerKey,
        WorkflowOutboundInterceptor next) : WorkflowOutboundInterceptor(next)
    {
        public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
            StartNexusOperationInput input)
        {
            if (context.Value is { } value)
            {
                Dictionary<string, string> headers =
                    input.Headers != null ? new(input.Headers!) : new(1);
                headers.Add(headerKey, value);
                input = input with { Headers = headers };
            }
            return base.StartNexusOperationAsync<TResult>(input);
        }
    }
}