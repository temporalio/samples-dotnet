using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkerSpecificTaskQueues;

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

    // Linked to the ctrl+c token so we can also stop both workers if one fails
    using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Executing workflow");
    await client.ExecuteWorkflowAsync(
        (FileProcessingWorkflow wf) => wf.RunAsync(5),
        new(id: "file-processing-0", taskQueue: "worker-specific-task-queues-sample"));
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Worker specific task queues sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
