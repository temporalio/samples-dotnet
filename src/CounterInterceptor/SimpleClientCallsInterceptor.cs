namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Client;
using Temporalio.Client.Interceptors;

public record ClientCounts
{
    public uint Executions { get; internal set; }

    public uint Signals { get; internal set; }

    public uint Queries { get; internal set; }

    public override string ToString() =>
        $"\n\tTotal Number of Workflow Exec: {Executions}\n\t" +
        $"Total Number of Signals: {Signals}\n\t" +
        $"Total Number of Queries: {Queries}";
}

public class SimpleClientCallsInterceptor : IClientInterceptor
{
    private const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    private const string NumberOfSignals = "numOfSignals";
    private const string NumberOfQueries = "numOfQueries";
    private static Dictionary<string, ClientCounts> clientDictionary = new();

    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
        new ClientOutbound(this, nextInterceptor);

    public string Info() =>
        string.Join(
            "\n",
            clientDictionary.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value}"));

    public uint NumOfWorkflowExecutions(string workflowId) =>
        clientDictionary[workflowId].Executions;

    public uint NumOfSignals(string workflowId) =>
        clientDictionary[workflowId].Signals;

    public uint NumOfQueries(string workflowId) =>
        clientDictionary[workflowId].Queries;

    private void Add(string workflowId, string type)
    {
        if (!clientDictionary.TryGetValue(workflowId, out ClientCounts? value))
        {
            value = new ClientCounts();
            clientDictionary.Add(workflowId, value);
        }

        switch (type)
        {
            case NumberOfWorkflowExecutions:
                value.Executions++;
                break;
            case NumberOfQueries:
                value.Queries++;
                break;
            case NumberOfSignals:
                value.Signals++;
                break;
            default:
                throw new NotImplementedException("Unknown type: " + type);
        }
    }

    private class ClientOutbound : ClientOutboundInterceptor
    {
        private SimpleClientCallsInterceptor root;

        public ClientOutbound(SimpleClientCallsInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            var id = input.Options.Id ?? "None";
            root.Add(id, NumberOfWorkflowExecutions);
            return base.StartWorkflowAsync<TWorkflow, TResult>(input);
        }

        public override Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            var id = input.Id ?? "None";
            root.Add(id, NumberOfSignals);
            return base.SignalWorkflowAsync(input);
        }

        public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            var id = input.Id ?? "None";
            root.Add(id, NumberOfQueries);
            return base.QueryWorkflowAsync<TResult>(input);
        }
    }
}