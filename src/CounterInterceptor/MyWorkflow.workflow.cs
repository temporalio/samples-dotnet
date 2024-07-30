namespace TemporalioSamples.CounterInterceptor;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    private bool exit; // Automatically defaults to false

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Wait for greeting info
        await Workflow.WaitConditionAsync(() => Name != null && Title != null);

        // Execute Child Workflow
        var result = await Workflow.ExecuteChildWorkflowAsync(
            (MyChildWorkflow wf) => wf.RunAsync(Name, Title),
            new() { Id = "counter-interceptor-child" });

        // Wait for exit signal
        await Workflow.WaitConditionAsync(() => exit);

        return result;
    }

    [WorkflowSignal]
    public async Task SignalNameAndTitleAsync(string name, string title)
    {
        Name = name;
        Title = title;
    }

    [WorkflowQuery]
    public string Name { get; private set; } = string.Empty;

    [WorkflowQuery]
    public string Title { get; private set; } = string.Empty;

    [WorkflowSignal]
    public async Task ExitAsync() => exit = true;
}