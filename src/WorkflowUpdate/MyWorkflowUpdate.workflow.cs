namespace TemporalioSamples.WorkflowUpdate;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflowUpdate
{
    private bool exit;

    private int result;

    [WorkflowRun]
    public async Task<int> RunAsync()
    {
        await Workflow.WaitConditionAsync(() => exit);
        return result;
    }

    [WorkflowUpdateValidator(nameof(AddValueAsync))]
    public void ValidatorAddValue(int inputValue)
    {
        if (inputValue < 0)
        {
            throw new ArgumentException("Input can not be a negative number");
        }
    }

    [WorkflowUpdate]
    public async Task<int> AddValueAsync(int inputValue)
    {
        result += inputValue;
        return result;
    }

    [WorkflowSignal]
    public async Task ExitAsync()
    {
        exit = true;
    }
}
