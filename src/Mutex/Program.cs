using System.CommandLine;
using System.Diagnostics;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Mutex;
using TemporalioSamples.Mutex.Impl;

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
        new TemporalWorkerOptions(taskQueue: "workflow-mutex-sample")
            .AddWorkflowMutex(client)
            .AddAllActivities(typeof(Activities), null)
            .AddWorkflow<WorkflowWithMutex>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

var resourceIdArgument = new Argument<string>("resource-id")
{
    Description = "Resource id to lock",
    DefaultValueFactory = _ => "locked-resource-id",
};

async Task ExecuteWorkflowsWithMutexAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();
    var resourceId = parseResult.GetRequiredValue(resourceIdArgument);

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

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute two workflows that contend for the same lock") { resourceIdArgument };
workflowCommand.SetAction(ExecuteWorkflowsWithMutexAsync);

var rootCommand = new RootCommand("Mutex sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
