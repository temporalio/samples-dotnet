using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.StandaloneActivity;

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

const string taskQueue = "standalone-activity-sample";

async Task RunWorkerAsync()
{
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue).
            AddActivity(MyActivities.ComposeGreetingAsync));
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteActivityAsync()
{
    var result = await client.ExecuteActivityAsync(
        () => MyActivities.ComposeGreetingAsync(new ComposeGreetingInput("Hello", "World")),
        new("standalone-activity-id", taskQueue)
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    Console.WriteLine($"Activity result: {result}");
}

async Task StartActivityAsync()
{
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

async Task ListActivitiesAsync()
{
    await foreach (var info in client.ListActivitiesAsync(
        $"TaskQueue = '{taskQueue}'"))
    {
        Console.WriteLine($"ActivityID: {info.ActivityId}, Type: {info.ActivityType}, Status: {info.Status}");
    }
}

async Task CountActivitiesAsync()
{
    var resp = await client.CountActivitiesAsync(
        $"TaskQueue = '{taskQueue}'");
    Console.WriteLine($"Total activities: {resp.Count}");
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "execute-activity":
        await ExecuteActivityAsync();
        break;
    case "start-activity":
        await StartActivityAsync();
        break;
    case "list-activities":
        await ListActivitiesAsync();
        break;
    case "count-activities":
        await CountActivitiesAsync();
        break;
    default:
        throw new ArgumentException(
            "Must pass 'worker', 'execute-activity', 'start-activity', 'list-activities', or 'count-activities' as the single argument");
}
