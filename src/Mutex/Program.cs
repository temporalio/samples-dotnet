using System.Diagnostics;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Mutex;
using TemporalioSamples.Mutex.Impl;

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").SetMinimumLevel(LogLevel.Information)),
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
        new TemporalWorkerOptions(taskQueue: "workflow-mutex-sample")
            .AddWorkflowMutex(client)
            .AddAllActivities(typeof(Activities), null)
            .AddWorkflow<WorkflowWithMutex>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowsWithMutexAsync(string resourceId)
{
    await Task.WhenAll(ExecuteAsync(), ExecuteAsync());

    return;

    async Task ExecuteAsync()
    {
        var workflowId = "test-" + Guid.NewGuid();
        Console.WriteLine($"Starting test workflow with id '{workflowId}'. Connecting to lock workflow '{resourceId}'");

        var sw = Stopwatch.StartNew();
        var handle = await client.StartWorkflowAsync(
            (WorkflowWithMutex wf) => wf.RunAsync(new WorkflowWithMutexInput(resourceId, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7.5))),
            new(workflowId, "workflow-mutex-sample"));

        Console.WriteLine($"Test workflow '{workflowId}' started");

        await handle.GetResultAsync();
        Console.WriteLine($"Test workflow '{workflowId}' finished after {sw.ElapsedMilliseconds}ms");
    }
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowsWithMutexAsync(args.ElementAtOrDefault(1) ?? "locked-resource-id");
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the first argument");
}
