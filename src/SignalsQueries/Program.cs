using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SignalsQueries;

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
    // If the workflow is already running from a previous run, terminate it
    try
    {
        await client.GetWorkflowHandle("signals-queries-workflow-id").TerminateAsync();
    }
    catch (Temporalio.Exceptions.RpcException ex) when (ex.Code == Temporalio.Exceptions.RpcException.StatusCode.NotFound)
    {
        // Ignore
    }

    Console.WriteLine("Executing workflow");
    var handle = await client.StartWorkflowAsync(
        (LoyaltyProgram wf) => wf.RunAsync("user-id-123"),
        new(id: "signals-queries-workflow-id", taskQueue: "signals-queries-sample"));

    Console.WriteLine("Signal: Purchase made for $80");
    await handle.SignalAsync(wf => wf.NotifyPurchaseAsync(new Purchase("purchase-1", 8_000)));
    Console.WriteLine("Signal: Purchase made for $40");
    await handle.SignalAsync(wf => wf.NotifyPurchaseAsync(new Purchase("purchase-1", 4_000)));

    // Wait for workflow to process the signals
    await Task.Delay(1000);
    var points = await handle.QueryAsync(wf => wf.Points);
    Console.WriteLine("Remaining points: {0}", points);
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