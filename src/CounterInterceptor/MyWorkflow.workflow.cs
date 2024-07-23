namespace TermporalioSamples.CounterInterceptor;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflow
{
    private string name = string.Empty;
    private string title = string.Empty;
    private bool exit; // automatically defaults to false

    [WorkflowRun]
    public async Task<string> ExecAsync()
    {
        // wait for greeting info
        await Workflow.WaitConditionAsync(() => name != null && title != null);

        // Execute Child Workflow
        string result = await Workflow.ExecuteChildWorkflowAsync(
            (MyChildWorkflow wf) => wf.ExecChildAsync(name, title),
            new()
            {
                Id = Constants.ChildWorkflowId,
            });

        // Wait for exit signal
        await Workflow.WaitConditionAsync(() => exit != false);

        return result;
    }

    [WorkflowSignal]
    public async Task SignalNameAndTitleAsync(string name, string title)
    {
        this.name = name;
        this.title = title;
    }

    [WorkflowQuery]
    public string QueryName()
    {
        return name;
    }

    [WorkflowQuery]
    public string QueryTitle()
    {
        return title;
    }

    [WorkflowSignal]
    public async Task ExitAsync()
    {
        this.exit = true;
    }
}