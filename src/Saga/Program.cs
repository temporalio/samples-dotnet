using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Saga;

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
if (string.IsNullOrEmpty(connectOptions.TargetHost))
{
    connectOptions.TargetHost = "localhost:7233";
}
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").SetMinimumLevel(LogLevel.Information));
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
        new TemporalWorkerOptions(taskQueue: "workflow-saga-sample")
            .AddAllActivities(typeof(Activities), null)
            .AddWorkflow<SagaWorkflow>());
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

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the first argument");
}
