namespace TemporalioSamples.CounterInterceptor;
public record WorkflowCounts
{
    public uint Executions { get; internal set; }

    public uint Signals { get; internal set; }

    public uint Queries { get; internal set; }

    public uint ChildExecutions { get; internal set; }

    public uint ActivityExecutions { get; internal set; }

    public override string ToString()
    {
        return
                "\n\tTotal Number of Workflow Exec: " + Executions +
                "\n\tTotal Number of Child Worflow Exec: " + ChildExecutions +
                "\n\tTotal Number of Activity Exec: " + ActivityExecutions +
                "\n\tTotal Number of Signals: " + Signals +
                "\n\tTotal Number of Queries: " + Queries;
    }
}

public static class WorkerCounter
{
    public const string NumberOfWorkflowExecutions = "numOfWorkflowExec";
    public const string NumberOfChildWorkflowExecutions = "numOfChildWorkflowExec";
    public const string NumberOfActivityExecutions = "numOfActivityExec";
    public const string NumberOfSignals = "numOfSignals";
    public const string NumberOfQueries = "numOfQueries";

    private static Dictionary<string, WorkflowCounts> perWorkflowIdDictionary =
        new Dictionary<string, WorkflowCounts>();

    public static void Add(string workflowId, string type)
    {
        if (!perWorkflowIdDictionary.TryGetValue(workflowId, out WorkflowCounts? value))
        {
            value = new WorkflowCounts();
            perWorkflowIdDictionary.Add(workflowId, value);
        }

        switch (type)
        {
            case NumberOfActivityExecutions:
                value.ActivityExecutions++;
                break;
            case NumberOfChildWorkflowExecutions:
                value.ChildExecutions++;
                break;
            case NumberOfQueries:
                value.Queries++;
                break;
            case NumberOfSignals:
                value.Signals++;
                break;
            case NumberOfWorkflowExecutions:
                value.Executions++;
                break;
            default:
                throw new NotImplementedException($"Unknown type: " + type);
        }
    }

    public static uint NumOfWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Executions;
    }

    public static uint NumOfChildWorkflowExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].ChildExecutions;
    }

    public static uint NumOfActivityExecutions(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].ActivityExecutions;
    }

    public static uint NumOfSignals(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Signals;
    }

    public static uint NumOfQueries(string workflowId)
    {
        return perWorkflowIdDictionary[workflowId].Queries;
    }

    public static string Info()
    {
        return string.Join(
            "\n",
            perWorkflowIdDictionary.Select(kvp => $"** Workflow ID: {kvp.Key} {kvp.Value}"));
    }
}