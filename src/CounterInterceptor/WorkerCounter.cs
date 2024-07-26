namespace TemporalioSamples.CounterInterceptor;

using System.Numerics;

public static class WorkerCounter
{
    public const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    public const string NumberOfChildWorkflowExecutions = "numOfChildWorkflowExec";
    public const string NumberOfActivityExecutions = "numOfActivityExec";
    public const string NumberOfSignals = "numOfSignals";
    public const string NumberOfQueries = "numOfQueries";

    private static Dictionary<string, Dictionary<string, uint>> perWorkflowIdDictionary =
        new Dictionary<string, Dictionary<string, uint>>();

    public static void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out Dictionary<string, uint>? value))
        {
            value = DefaultInfoMap();
            perWorkflowIdDictionary.Add(workflowId, value);
        }

        var current = value[type];
        var next = current + 1;
        value[type] = next;
    }

    public static uint NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfWorkflowExecutions];
    }

    public static uint NumOfChildWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfChildWorkflowExecutions];
    }

    public static uint NumOfActivityExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfActivityExecutions];
    }

    public static uint NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfSignals];
    }

    public static uint NumOfQueries(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId][NumberOfQueries];
    }

    public static string Info()
    {
        string result = string.Empty;
        foreach (var item in perWorkflowIdDictionary)
        {
            var itemInfo = item.Value;
            result = result +
                "\n** Workflow ID: " + item.Key +
                "\n\tTotal Number of Workflow Exec: " + itemInfo[NumberOfWorkflowExecutions] +
                "\n\tTotal Number of Child Worflow Exec: " + itemInfo[NumberOfChildWorkflowExecutions] +
                "\n\tTotal Number of Activity Exec: " + itemInfo[NumberOfActivityExecutions] +
                "\n\tTotal Number of Signals: " + itemInfo[NumberOfSignals] +
                "\n\tTotal Number of Queries: " + itemInfo[NumberOfQueries];
        }

        return result;
    }

    // Creates a default counter info map for a workflowId
    private static Dictionary<string, uint> DefaultInfoMap()
    {
        return new Dictionary<string, uint>()
        {
            { NumberOfWorkflowExecutions, 0 },
            { NumberOfChildWorkflowExecutions, 0 },
            { NumberOfActivityExecutions, 0 },
            { NumberOfSignals, 0 },
            { NumberOfQueries, 0 },
        };
    }
}