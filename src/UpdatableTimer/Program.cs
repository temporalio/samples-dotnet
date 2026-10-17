using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.UpdatableTimer;

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
        new TemporalWorkerOptions(taskQueue: "updatable-timer").
            AddWorkflow<MyWorkflow>());
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
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(DateTimeOffset.UtcNow.AddDays(1)),
        new(id: "updatable-timer-workflow-id", taskQueue: "updatable-timer"));
}

async Task UpdateTimerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = client.GetWorkflowHandle<MyWorkflow>("updatable-timer-workflow-id");
    // signal workflow about the wake-up time change
    await handle.SignalAsync(workflow => workflow.UpdateWakeUpAsync(DateTimeOffset.UtcNow.AddSeconds(10)));
    Console.WriteLine("Updated wake up time to 10 seconds from now");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var updateTimerCommand = new Command("update-timer", "Signal workflow to wake up in 10 seconds");
updateTimerCommand.SetAction(UpdateTimerAsync);

var rootCommand = new RootCommand("Updatable timer sample") { workerCommand, workflowCommand, updateTimerCommand };
return await rootCommand.Parse(args).InvokeAsync();
