namespace TemporalioSamples.CounterInterceptor;

using System.Numerics;

public class ClientCounter
{
    private const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    private const string NumberOfSignals = "numOfSignals";
    private const string NumberOfQueries = "numOfQueries";
    private static Dictionary<string, Dictionary<string, uint>> perWorkflowIdDictionary =
        new();

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

    public static uint NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfWorkflowExecutions];
    }

    public static uint NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfSignals];
    }

    public static uint NumOfQueries(string workflowId)
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
    private static Dictionary<string, uint> GetDefaultInfoMap()
    {
        return new Dictionary<string, uint>()
        {
            { NumberOfWorkflowExecutions, 0 },
            { NumberOfSignals, 0 },
            { NumberOfQueries, 0 },
        };
    }

    private void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out Dictionary<string, uint>? value))
        {
            value = GetDefaultInfoMap();
            perWorkflowIdDictionary.Add(workflowId, value);
        }

        var current = value[type];
        var next = current + 1;
        value[type] = next;
    }
}