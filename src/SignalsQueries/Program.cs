using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.SignalsQueries;

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
        new TemporalWorkerOptions(taskQueue: "signals-queries-sample").
            AddActivity(MyActivities.SendCoupon).
            AddWorkflow<LoyaltyProgram>());
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
        (LoyaltyProgram wf) => wf.RunAsync("user-id-123"),
        new(id: "signals-queries-workflow-id", taskQueue: "signals-queries-sample"));

    Console.WriteLine("Signal: Purchase made for $80");
    await handle.SignalAsync(wf => wf.NotifyPurchaseAsync(8_000));
    Console.WriteLine("Signal: Purchase made for $30");
    await handle.SignalAsync(wf => wf.NotifyPurchaseAsync(3_000));

    var points = await handle.QueryAsync(wf => wf.Points);
    Console.WriteLine("Remaining points: {Points}", points);
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