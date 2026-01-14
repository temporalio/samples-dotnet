using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkerSpecificTaskQueues;

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

    var uniqueWorkerTaskQueue = Guid.NewGuid().ToString();

    var normalActivities = new NormalActivities(uniqueWorkerTaskQueue);

    // Run worker until cancelled
    Console.WriteLine("Running worker");

    using var normalWorker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "worker-specific-task-queues-sample")
            .AddActivity(normalActivities.GetUniqueTaskQueue)
            .AddWorkflow<FileProcessingWorkflow>());

    using var uniqueTaskQueueWorker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: uniqueWorkerTaskQueue)
            .AddActivity(WorkerSpecificActivities.DownloadFileToWorkerFileSystemAsync)
            .AddActivity(WorkerSpecificActivities.CleanupFileFromWorkerFileSystemAsync)
            .AddActivity(WorkerSpecificActivities.WorkOnFileInWorkerFileSystemAsync));

    var tasks = new List<Task> { normalWorker.ExecuteAsync(tokenSource.Token), uniqueTaskQueueWorker.ExecuteAsync(tokenSource.Token) };

    var task = await Task.WhenAny(tasks);
    if (task.Exception is not null)
    {
        try
        {
            await tokenSource.CancelAsync();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled ex {ex}");
            throw;
        }
    }
    else
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
    Console.WriteLine("Executing workflow");
    await client.ExecuteWorkflowAsync(
        (FileProcessingWorkflow wf) => wf.RunAsync(5),
        new(id: "file-processing-0", taskQueue: "worker-specific-task-queues-sample"));
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