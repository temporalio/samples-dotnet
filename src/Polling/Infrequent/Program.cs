using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Polling.Infrequent;

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

    // Create an activity instance with some state
    var activities = new InfrequentPollingActivities(new());

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "infrequent-polling-sample")
            .AddActivity(activities.DoPollAsync)
            .AddWorkflow<InfrequentPollingWorkflow>());
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

    var result = await client.ExecuteWorkflowAsync(
        (InfrequentPollingWorkflow wf) => wf.RunAsync(),
        new(id: "infrequent-polling-sample-workflow-id", taskQueue: "infrequent-polling-sample"));

    Console.WriteLine($"Result: {result}");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Infrequent polling sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
