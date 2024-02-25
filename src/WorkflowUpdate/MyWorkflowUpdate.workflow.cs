namespace TemporalioSamples.WorkflowUpdate;

using Temporalio.Workflows;

[Workflow]
public class MyWorkflowUpdate
{
    private readonly Queue<UiRequest> requests = new();
    private ScreenId screen = ScreenId.Screen1;

    [WorkflowRun]
    public async Task RunAsync()
    {
        while (!IsLatestScreen())
        {
            await Workflow.WaitConditionAsync(() => requests.Any());
            var currentRequest = requests.Peek();
            SetNextScreen(currentRequest);
            requests.Dequeue();
        }

        // TODO if I remove this the test (RunAsync_SimpleRun_Succeeds) fails,
        // I think it is because the workflow completes
        // before the update handler returns the value?
        await Workflow.WaitConditionAsync(() => true);
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

        requests.Enqueue(request);

        await Workflow.WaitConditionAsync(() => !requests.Contains(request));

        return GetCurrentScreen();
    }

    [WorkflowQuery]
    public ScreenId GetCurrentScreen()
    {
        return screen;
    }

    private bool IsLatestScreen()
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