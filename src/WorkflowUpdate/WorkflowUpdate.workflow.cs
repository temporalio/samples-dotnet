namespace TemporalioSamples.WorkflowUpdate;

using Temporalio.Workflows;

[Workflow]
public class WorkflowUpdate
{
    private bool updateInProgress;
    private ScreenId currentScreen = ScreenId.Screen1;

    [WorkflowRun]
    public async Task RunAsync()
    {
        await Workflow.WaitConditionAsync(() => currentScreen == ScreenId.End);
    }

    [WorkflowUpdateValidator(nameof(SubmitScreenAsync))]
    public void ValidatorSubmitScreen(UiRequest request)
    {
        if (request == null)
        {
            throw new ArgumentException("Input can not be null");
        }
    }

    [WorkflowUpdate]
    public async Task<ScreenId> SubmitScreenAsync(UiRequest request)
    {
        // Ensure we process the requests one by one
        await Workflow.WaitConditionAsync(() => !updateInProgress);
        updateInProgress = true;

        // Activities can be scheduled here
        SetNextScreen(request);
        updateInProgress = false;
        return currentScreen;
    }

    private void SetNextScreen(UiRequest currentRequest)
    {
        currentScreen = currentRequest.ScreenId switch {
            ScreenId.Screen1 => ScreenId.Screen2,
            ScreenId.Screen2 => ScreenId.End,
            _ => currentScreen,
        };
    }

    public enum ScreenId
    {
        Screen1,
        Screen2,
        End,
    }

    public record UiRequest(string RequestId, ScreenId ScreenId);
}