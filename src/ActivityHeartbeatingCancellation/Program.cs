using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.ActivityHeartbeatingCancellation;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "activity-heartbeating-cancellation-sample")
            .AddActivity(MyActivities.FakeProgressAsync)
            .AddWorkflow<MyWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAndThenCancelAsync()
{
    Console.WriteLine("Executing workflow");

    var handle = await client.StartWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "activity-heartbeating-cancellation-workflow-id", taskQueue: "activity-heartbeating-cancellation-sample"));

    // Simulate waiting for some time.
    // Cancel may be immediately called, waiting is not needed
    await Task.Delay(TimeSpan.FromSeconds(20));

    await handle.CancelAsync();
    Console.WriteLine("Cancelled workflow successfully");

    try
    {
        await handle.GetResultAsync();
    }
    catch (WorkflowFailedException e) when (e.InnerException is CancelledFailureException)
    {
        Console.WriteLine("await handle.GetResultAsync() threw because Workflow was cancelled");
    }
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAndThenCancelAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}