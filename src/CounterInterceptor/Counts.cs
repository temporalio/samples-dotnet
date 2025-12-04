namespace TemporalioSamples.CounterInterceptor;

public class Counts
{
    private uint clientExecutions;
    private uint clientQueries;
    private uint clientSignals;
    private uint workflowReplays;
    private uint workflowSignals;
    private uint workflowQueries;
    private uint workflowChildExecutions;
    private uint workflowActivityExecutions;

    public ref uint ClientExecutions => ref clientExecutions;

    public ref uint ClientSignals => ref clientSignals;

    public ref uint ClientQueries => ref clientQueries;

    public string ClientInfo() =>
         $"\n\tTotal Number of Workflow Exec: {ClientExecutions}\n\t" +
        $"Total Number of Signals: {ClientSignals}\n\t" +
        $"Total Number of Queries: {ClientQueries}";

    public ref uint WorkflowReplays => ref workflowReplays;

    public ref uint WorkflowSignals => ref workflowSignals;

    public ref uint WorkflowQueries => ref workflowQueries;

    public ref uint WorkflowChildExecutions => ref workflowChildExecutions;

    public ref uint WorkflowActivityExecutions => ref workflowActivityExecutions;

    public string WorkflowInfo() =>
        $"\n\tTotal Number of Workflow Replays: {WorkflowReplays}\n\t" +
        $"Total Number of Child Workflow Exec: {WorkflowChildExecutions}\n\t" +
        $"Total Number of Activity Exec: {WorkflowActivityExecutions}\n\t" +
        $"Total Number of Signals: {WorkflowSignals}\n\t" +
        $"Total Number of Queries: {WorkflowQueries}";

    public override string ToString() =>
        ClientInfo() + WorkflowInfo();
}