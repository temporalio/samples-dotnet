namespace TermporalioSamples.CounterInterceptor;

using System.Numerics;

public class ClientCounter
{
    private const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    private const string NumberOfSignals = "numOfSignals";
    private const string NumberOfQueries = "numOfQueries";
    private static Dictionary<string, Dictionary<string, BigInteger?>> perWorkflowIdDictionary =
        new Dictionary<string, Dictionary<string, BigInteger?>>();

    public static string Info()
    {
        string result = string.Empty;
        foreach (var item in perWorkflowIdDictionary)
        {
            var info = item.Value;
            result = result +
                "\n** Workflow ID: " + item.Key +
                "\n\tTotal Number of Workflow Exec: " + info[NumberOfWorkflowExecutions] +
                "\n\tTotal Number of Signals: " + info[NumberOfSignals] +
                "\n\tTotal Number of Queries: " + info[NumberOfQueries];
        }

        return result;
    }

    public BigInteger? NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfWorkflowExecutions];
    }

    public BigInteger? NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfSignals];
    }

    public BigInteger? NumOfQueries(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfQueries];
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

    // Creates a default counter info map for a workflowid
    private static Dictionary<string, BigInteger?> GetDefaultInfoMap()
    {
        return new Dictionary<string, BigInteger?>()
        {
            { NumberOfWorkflowExecutions, 0 },
            { NumberOfSignals, 0 },
            { NumberOfQueries, 0 },
        };
    }

    private void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out Dictionary<string, BigInteger?>? value))
        {
            value = GetDefaultInfoMap();
            perWorkflowIdDictionary.Add(workflowId, value);
        }

        if (value[type] == null)
        {
            value[type] = 1;
        }
        else
        {
            var current = value[type];
            var next = current + 1;
            value[type] = next;
        }
    }
}