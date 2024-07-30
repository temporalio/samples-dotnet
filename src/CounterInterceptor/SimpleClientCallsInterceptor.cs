namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Client;
using Temporalio.Client.Interceptors;

public class SimpleClientCallsInterceptor : IClientInterceptor
{
    private ClientCounter clientCounter;

    public SimpleClientCallsInterceptor()
    {
        this.clientCounter = new ClientCounter();
    }

    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
        new ClientOutbound(this, nextInterceptor);

    private class ClientOutbound : ClientOutboundInterceptor
    {
        private SimpleClientCallsInterceptor root;

        public ClientOutbound(SimpleClientCallsInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            var id = CheckId(input.Options.Id);
            root.clientCounter.AddStartInvocation(id);
            return base.StartWorkflowAsync<TWorkflow, TResult>(input);
        }

        public override Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            var id = CheckId(input.Id);
            root.clientCounter.AddSignalInvocation(id);
            return base.SignalWorkflowAsync(input);
        }

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            var id = CheckId(input.Id);
            root.clientCounter.AddQueryInvocation(id);
            return base.QueryWorkflowAsync<TResult>(input);
        }

        public override Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
            StartWorkflowUpdateInput input)
        {
            // Not tracking this
            return base.StartWorkflowUpdateAsync<TResult>(input);
        }

        private static string CheckId(string? id)
        {
            return id ??= "None";
        }
    }
}