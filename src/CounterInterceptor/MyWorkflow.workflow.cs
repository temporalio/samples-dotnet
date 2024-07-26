namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    private string name = string.Empty;
    private string title = string.Empty;
    private bool exit; // automatically defaults to false

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // wait for greeting info
        await Workflow.WaitConditionAsync(() => name != null && title != null);

        // Execute Child Workflow
        var result = await Workflow.ExecuteChildWorkflowAsync(
            (MyChildWorkflow wf) => wf.RunAsync(name, title),
            new() { Id = "counter-interceptor-child" });

        // Wait for exit signal
        await Workflow.WaitConditionAsync(() => exit);

        return result;
    }

    [WorkflowSignal]
    public async Task SignalNameAndTitleAsync(string name, string title)
    {
        this.name = name;
        this.title = title;
    }

    [WorkflowQuery]
    public string Name { get; private set; } = string.Empty;

    [WorkflowQuery]
    public string Title { get; private set; } = string.Empty;

    [WorkflowSignal]
    public async Task ExitAsync() => exit = true;
}