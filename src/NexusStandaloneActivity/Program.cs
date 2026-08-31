using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusStandaloneActivity;
using TemporalioSamples.NexusStandaloneActivity.Handler;

const string taskQueue = "nexus-handler-queue";

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

Task<TemporalClient> ConnectClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = loggerFactory;
    return TemporalClient.ConnectAsync(connectOptions);
}

// Worker that hosts the Nexus service implementation and the Activity backing its operation. The
// task queue must match the Nexus endpoint's target task queue (see README).
async Task RunWorkerAsync()
{
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    logger.LogInformation("Running worker on task queue {TaskQueue}", taskQueue);
    using var worker = new TemporalWorker(
        await ConnectClientAsync(),
        new TemporalWorkerOptions(taskQueue).
            AddNexusService(new GreetingService()).
            AddActivity(GreetingActivities.CreateGreetingAsync));
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

// Executes the Activity-backed Nexus Operation from client code. The operation is standalone: it is
// started directly by this client rather than from within a caller Workflow.
async Task RunStarterAsync()
{
    var client = await ConnectClientAsync();

    // Create a Nexus client bound to the endpoint and service. The endpoint must be pre-created on
    // the server (see README).
    var nexusClient = client.CreateNexusClient<IGreetingService>(NexusEndpoints.GreetingService);

    var handle = await nexusClient.StartNexusOperationAsync(
        svc => svc.Greet(new("World")),
        new($"greeting-{Guid.NewGuid()}")
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    logger.LogInformation("Started Greet operation OperationID {OperationId}", handle.Id);

    var result = await handle.GetResultAsync();
    logger.LogInformation("Greet result: {Message}", result.Message);
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "starter":
        await RunStarterAsync();
        break;
    default:
        throw new ArgumentException(
            "Must pass 'worker' or 'starter' as the single argument");
}
