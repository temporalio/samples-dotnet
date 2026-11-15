using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkflowUpdate;

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
        new TemporalWorkerOptions(taskQueue: "workflow-update-queue").
            AddWorkflow<WorkflowUpdate>());
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

    Console.WriteLine("Executing workflow");

    var handle = await client.StartWorkflowAsync(
        (WorkflowUpdate wf) => wf.RunAsync(),
        new(id: $"workflow-update-{Guid.NewGuid()}", taskQueue: "workflow-update-queue"));

    await handle.ExecuteUpdateAsync(wf =>
        wf.SubmitScreenAsync(new WorkflowUpdate.UiRequest(
            $"requestId-{Guid.NewGuid()}",
            WorkflowUpdate.ScreenId.Screen1)));

    await handle.ExecuteUpdateAsync(wf =>
        wf.SubmitScreenAsync(new WorkflowUpdate.UiRequest(
            $"requestId-{Guid.NewGuid()}",
            WorkflowUpdate.ScreenId.Screen2)));

    // Workflow completes
    await handle.GetResultAsync();
    Console.WriteLine("Workflow completes");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Workflow update sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
