using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.WorkflowUpdate;

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
        new TemporalWorkerOptions(taskQueue: "workflow-update-queue").
            AddWorkflow<WorkflowUpdate>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
    Console.WriteLine("Executing workflow");

    var handle = await client.StartWorkflowAsync(
        (WorkflowUpdate wf) => wf.RunAsync(),
        new(id: $"workflow-update-{Guid.NewGuid()}", taskQueue: "workflow-update-queue"));

    await handle.ExecuteUpdateAsync(wf =>
        wf.SubmitScreenAsync(new WorkflowUpdate.UiRequest(
            $"requestId-{Guid.NewGuid()}",
            WorkflowUpdate.ScreenId.Screen1)));

    await handle.ExecuteUpdateAsync(wf =>
        wf.SubmitScreenAsync(new WorkflowUpdate.UiRequest(
            $"requestId-{Guid.NewGuid()}",
            WorkflowUpdate.ScreenId.Screen2)));

    // Workflow completes
    await handle.GetResultAsync();
    Console.WriteLine("Workflow completes");
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}