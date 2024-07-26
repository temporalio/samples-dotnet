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
    public async Task<string> RunAsync(string name, string title) =>
        await Workflow.ExecuteActivityAsync((MyActivities act) => act.SayHello(name, title), activityOptions) +
        await Workflow.ExecuteActivityAsync((MyActivities act) => act.SayGoodBye(name, title), activityOptions);
}