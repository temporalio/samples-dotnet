namespace TemporalioSamples.CounterInterceptor;

using System.Numerics;

public static class WorkerCounter
{
    public const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    public const string NumberOfChildWorkflowExecutions = "numOfChildWorkflowExec";
    public const string NumberOfActivityExecutions = "numOfActivityExec";
    public const string NumberOfSignals = "numOfSignals";
    public const string NumberOfQueries = "numOfQueries";

    private static Dictionary<string, Dictionary<string, BigInteger?>> perWorkflowIdDictionary =
        new Dictionary<string, Dictionary<string, BigInteger?>>();

    public static void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out Dictionary<string, BigInteger?>? value))
        {
            value = DefaultInfoMap();
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

    public static BigInteger? NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfWorkflowExecutions];
    }

    public static BigInteger? NumOfChildWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfChildWorkflowExecutions];
    }

    public static BigInteger? NumOfActivityExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfActivityExecutions];
    }

    public static BigInteger? NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfSignals];
    }

    public static BigInteger? NumOfQueries(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfQueries];
    }

    public static string Info()
    {
        string result = string.Empty;
        foreach (var item in perWorkflowIdDictionary)
        {
            var info = item.Value;
            result = result +
                "\n** Workflow ID: " + item.Key +
                "\n\tTotal Number of Workflow Exec: " + info[NumberOfWorkflowExecutions] +
                "\n\tTotal Number of Child Worflow Exec: " + info[NumberOfChildWorkflowExecutions] +
                "\n\tTotal Number of Activity Exec: " + info[NumberOfActivityExecutions] +
                "\n\tTotal Number of Signals: " + info[NumberOfSignals] +
                "\n\tTotal Number of Queries: " + info[NumberOfQueries];
        }

        return result;
    }

    // Creates a default counter info map for a workflowid
    private static Dictionary<string, BigInteger?> DefaultInfoMap()
    {
        return new Dictionary<string, BigInteger?>()
        {
            { NumberOfWorkflowExecutions, 0 },
            { NumberOfChildWorkflowExecutions, 0 },
            { NumberOfActivityExecutions, 0 },
            { NumberOfSignals, 0 },
            { NumberOfQueries, 0 },
        };
    }
}