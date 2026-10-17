using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Saga;

async Task<TemporalClient> ConnectClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");

    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "workflow-saga-sample")
            .AddAllActivities(typeof(Activities), null)
            .AddWorkflow<SagaWorkflow>());
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

    var workflowId = "test-" + Guid.NewGuid();
    Console.WriteLine($"Starting test workflow with id '{workflowId}'.");

    var sw = Stopwatch.StartNew();
    var handle = await client.StartWorkflowAsync(
        (SagaWorkflow wf) => wf.RunAsync(new TransferDetails(100, "acc1000", "acc2000", "1324")),
        new(workflowId, "workflow-saga-sample"));

    Console.WriteLine($"Test workflow '{workflowId}' started");

    await handle.GetResultAsync();
    Console.WriteLine($"Test workflow '{workflowId}' finished after {sw.ElapsedMilliseconds}ms");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Saga sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
