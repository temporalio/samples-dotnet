using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Batch.Iterator;

const string TaskQueue = "IteratorBatch";

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
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

    // Run worker until cancelled. Hosts both the IteratorBatchWorkflow and the
    // RecordProcessorWorkflow it starts as children, plus the RecordLoaderActivities activity.
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: TaskQueue)
            .AddAllActivities(typeof(RecordLoaderActivities), null)
            .AddWorkflow<IteratorBatchWorkflow>()
            .AddWorkflow<RecordProcessorWorkflow>());
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
    var workflowId = "iterator-batch-" + Guid.NewGuid();
    Console.WriteLine($"Starting batch workflow with id '{workflowId}'.");

    var handle = await client.StartWorkflowAsync(
        (IteratorBatchWorkflow wf) => wf.RunAsync(5, 0),
        new(workflowId, TaskQueue));

    Console.WriteLine($"Started batch workflow. WorkflowId={handle.Id}, RunId={handle.ResultRunId}");
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
