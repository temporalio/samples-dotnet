namespace TemporalioSamples.CounterInterceptor;
public record ClientCounts
{
    public uint Executions { get; internal set; }

    public uint Signals { get; internal set; }

    public uint Queries { get; internal set; }

    public override string ToString()
    {
        return
                "\n\tTotal Number of Workflow Exec: " + Executions +
                "\n\tTotal Number of Signals: " + Signals +
                "\n\tTotal Number of Queries: " + Queries;
    }
}

public class ClientCounter
{
    private const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    private const string NumberOfSignals = "numOfSignals";
    private const string NumberOfQueries = "numOfQueries";
    private static Dictionary<string, ClientCounts> perWorkflowIdDictionary =
        new();

    public static string Info()
    {
        return string.Join(
            "\n",
            perWorkflowIdDictionary.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value}"));
    }

    public static uint NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Executions;
    }

    public static uint NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Signals;
    }

    public static uint NumOfQueries(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Queries;
    }

    public void AddStartInvocation(string workflowId)
    {
        Add(workflowId, NumberOfWorkflowExecutions);
    }

    public void AddSignalInvocation(string workflowId)
    {
        Add(workflowId, NumberOfSignals);
    }

    public void AddQueryInvocation(string workflowId)
    {
        Add(workflowId, NumberOfQueries);
    }

    private void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out ClientCounts? value))
        {
            value = new ClientCounts();
            perWorkflowIdDictionary.Add(workflowId, value);
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
}