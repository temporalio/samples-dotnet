using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.ActivityHeartbeatingCancellation;

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
        new TemporalWorkerOptions(taskQueue: "activity-heartbeating-cancellation-sample")
            .AddActivity(MyActivities.FakeProgressAsync)
            .AddWorkflow<MyWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAndThenCancelAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Executing workflow");

    var handle = await client.StartWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "activity-heartbeating-cancellation-workflow-id", taskQueue: "activity-heartbeating-cancellation-sample"));

    // Simulate waiting for some time.
    // Cancel may be immediately called, waiting is not needed
    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

    await handle.CancelAsync();
    Console.WriteLine("Cancelled workflow successfully");

    try
    {
        await handle.GetResultAsync();
    }
    catch (WorkflowFailedException e) when (e.InnerException is CanceledFailureException)
    {
        Console.WriteLine("await handle.GetResultAsync() threw because Workflow was cancelled");
    }
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow and then cancel it");
workflowCommand.SetAction(ExecuteWorkflowAndThenCancelAsync);

var rootCommand = new RootCommand("Activity heartbeating cancellation sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
