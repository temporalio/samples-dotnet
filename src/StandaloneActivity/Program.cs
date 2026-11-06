using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.StandaloneActivity;

async Task<TemporalClient> ConnectClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

const string taskQueue = "standalone-activity-sample";

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue).
            AddActivity(MyActivities.ComposeGreetingAsync));
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteActivityAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var result = await client.ExecuteActivityAsync(
        () => MyActivities.ComposeGreetingAsync(new ComposeGreetingInput("Hello", "World")),
        new("standalone-activity-id", taskQueue)
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    Console.WriteLine($"Activity result: {result}");
}

async Task StartActivityAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var handle = await client.StartActivityAsync(
        () => MyActivities.ComposeGreetingAsync(new ComposeGreetingInput("Hello", "World")),
        new("standalone-activity-id", taskQueue)
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    Console.WriteLine($"Started activity: {handle.Id}");

    var result = await handle.GetResultAsync();
    Console.WriteLine($"Activity result: {result}");
}

async Task ListActivitiesAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    await foreach (var info in client.ListActivitiesAsync(
        $"TaskQueue = '{taskQueue}'"))
    {
        Console.WriteLine($"ActivityID: {info.ActivityId}, Type: {info.ActivityType}, Status: {info.Status}");
    }
}

async Task CountActivitiesAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var resp = await client.CountActivitiesAsync(
        $"TaskQueue = '{taskQueue}'");
    Console.WriteLine($"Total activities: {resp.Count}");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var executeActivityCommand = new Command("execute-activity", "Execute activity and wait for result");
executeActivityCommand.SetAction(ExecuteActivityAsync);

var startActivityCommand = new Command("start-activity", "Start activity and then wait for result");
startActivityCommand.SetAction(StartActivityAsync);

var listActivitiesCommand = new Command("list-activities", "List activities");
listActivitiesCommand.SetAction(ListActivitiesAsync);

var countActivitiesCommand = new Command("count-activities", "Count activities");
countActivitiesCommand.SetAction(CountActivitiesAsync);

var rootCommand = new RootCommand("Standalone activity sample")
{
    workerCommand,
    executeActivityCommand,
    startActivityCommand,
    listActivitiesCommand,
    countActivitiesCommand,
};
return await rootCommand.Parse(args).InvokeAsync();
