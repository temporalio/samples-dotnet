namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Workflows;

[Workflow]
public class MyChildWorkflow
{
    private readonly ActivityOptions activityOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(10),
    };

    [WorkflowRun]
    public async Task<string> ExecChildAsync(string name, string title)
    {
        string result = await Workflow.ExecuteActivityAsync((MyActivities act) => act.SayHello(name, title), activityOptions);
        result += await Workflow.ExecuteActivityAsync((MyActivities act) => act.SayGoodBye(name, title), activityOptions);

        return result;
    }
}