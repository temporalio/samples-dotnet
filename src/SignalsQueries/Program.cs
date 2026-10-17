using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SignalsQueries;

async Task<TemporalClient> ConnectClientAsync()
{
    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "signals-queries-sample").
            AddActivity(MyActivities.SendCoupon).
            AddWorkflow<LoyaltyProgram>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

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
    await Task.Delay(1000, cancellationToken);
    var points = await handle.QueryAsync(wf => wf.Points);
    Console.WriteLine("Remaining points: {0}", points);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Signals and queries sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
