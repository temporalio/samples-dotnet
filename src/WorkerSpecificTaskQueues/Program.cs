using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ActivityStickyQueues;

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

    var uniqueWorkerTaskQueue = Guid.NewGuid().ToString();

    var normalActivities = new NormalActivities(uniqueWorkerTaskQueue);

    // Run worker until cancelled
    Console.WriteLine("Running worker");

    using var nonStickyWorker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "activity-sticky-queues-sample")
            .AddActivity(normalActivities.GetUniqueTaskQueue)
            .AddWorkflow<FileProcessingWorkflow>());

    using var stickyWorker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: uniqueWorkerTaskQueue)
            .AddActivity(StickyActivities.DownloadFileToWorkerFileSystemAsync)
            .AddActivity(StickyActivities.CleanupFileFromWorkerFileSystemAsync)
            .AddActivity(StickyActivities.WorkOnFileInWorkerFileSystemAsync));

    var tasks = new List<Task> { nonStickyWorker.ExecuteAsync(tokenSource.Token), stickyWorker.ExecuteAsync(tokenSource.Token) };

    var task = await Task.WhenAny(tasks);
    if (task.Exception is not null)
    {
        try
        {
            tokenSource.Cancel();
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
        new(id: "file-processing-0", taskQueue: "activity-sticky-queues-sample"));
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