namespace TemporalioSamples.WorkflowUpdate;

using Temporalio.Workflows;

[Workflow]
public class WorkflowUpdate
{
    private readonly Queue<UiRequest> requests = new();
    private ScreenId screen = ScreenId.Screen1;
    private bool updateInProgress;

    [WorkflowRun]
    public async Task RunAsync()
    {
        while (!IsLastScreen())
        {
            // watch eventHistoryLength (Workflow.CurrentHistoryLength) and CAN if > 10000
            await Workflow.WaitConditionAsync(() => requests.Any());
            var currentRequest = requests.Peek();
            SetNextScreen(currentRequest);
            requests.Dequeue();
        }

        // Ensure update method has completed
        await Workflow.WaitConditionAsync(() => !updateInProgress);
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
        await Workflow.WaitConditionAsync(() => !requests.Any());
        updateInProgress = true;

        requests.Enqueue(request);

        await Workflow.WaitConditionAsync(() => !requests.Contains(request));
        updateInProgress = false;

        return GetCurrentScreen();
    }

    [WorkflowQuery]
    public ScreenId GetCurrentScreen()
    {
        return screen;
    }

    private bool IsLastScreen()
    {
        return GetCurrentScreen() == ScreenId.End;
    }

    private void SetNextScreen(UiRequest currentRequest)
    {
        screen = currentRequest.ScreenId switch {
            ScreenId.Screen1 => ScreenId.Screen2,
            ScreenId.Screen2 => ScreenId.End,
            _ => screen,
        };
    }
}