using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Polling.PeriodicSequence;

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

    // Create an activity instance with some state
    var activity = new PeriodicPollingActivity(new(errorAttempts: 50));

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "periodic-sequence-polling-sample")
            .AddActivity(activity.DoPollAsync)
            .AddWorkflow<PeriodicPollingWorkflow>()
            .AddWorkflow<PeriodicPollingChildWorkflow>());
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

    var result = await client.ExecuteWorkflowAsync(
        (PeriodicPollingWorkflow wf) => wf.RunAsync(),
        new(id: "periodic-sequence-polling-sample-workflow-id", taskQueue: "periodic-sequence-polling-sample"));

    Console.WriteLine($"Result: {result}");
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