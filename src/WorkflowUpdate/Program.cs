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
            AddWorkflow<MyWorkflowUpdate>());
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
        (MyWorkflowUpdate wf) => wf.RunAsync(),
        new(id: "workflow-update-id", taskQueue: "workflow-update-queue"));

    try
    {
        // The update request will fail on a negative number and the exception will be thrown here.
        await handle.ExecuteUpdateAsync(
            (MyWorkflowUpdate wf) => wf.AddValueAsync(-1));
    }
    catch (WorkflowUpdateFailedException e)
    {
        Console.WriteLine("Update failed, cause:  " + e);
    }
    for (int i = 0; i < 4; i++)
    {
        await handle.ExecuteUpdateAsync(
            (MyWorkflowUpdate wf) => wf.AddValueAsync(i));
    }

    await handle.SignalAsync(
        (MyWorkflowUpdate wf) => wf.ExitAsync());

    Console.WriteLine("Result: " + await handle.GetResultAsync());
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