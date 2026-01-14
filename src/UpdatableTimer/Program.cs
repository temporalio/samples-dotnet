using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.UpdatableTimer;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

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
        new TemporalWorkerOptions(taskQueue: "updatable-timer").
            AddWorkflow<MyWorkflow>());
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
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(DateTimeOffset.UtcNow.AddDays(1)),
        new(id: "updatable-timer-workflow-id", taskQueue: "updatable-timer"));
}

async Task UpdateTimerAsync()
{
    var handle = client.GetWorkflowHandle<MyWorkflow>("updatable-timer-workflow-id");
    // signal workflow about the wake-up time change
    await handle.SignalAsync(workflow => workflow.UpdateWakeUpAsync(DateTimeOffset.UtcNow.AddSeconds(10)));
    Console.WriteLine("Updated wake up time to 10 seconds from now");
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    case "update-timer":
        await UpdateTimerAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker', 'workflow' or 'update-timer' as the single argument");
}