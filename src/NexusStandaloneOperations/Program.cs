using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusStandaloneOperations;
using TemporalioSamples.NexusStandaloneOperations.Handler;

const string taskQueue = "nexus-standalone-operations";

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
            AddNexusService(new HelloService()).
            AddWorkflow<HelloHandlerWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

async Task RunStarterAsync()
{
    var client = await ConnectClientAsync();

    // Create a Nexus client bound to the endpoint and service.
    // The endpoint must be pre-created on the server (see README).
    var nexusClient = client.CreateNexusClient<IHelloService>(IHelloService.EndpointName);

    // Execute the sync Echo operation.
    var echoHandle = await nexusClient.StartNexusOperationAsync(
        svc => svc.Echo(new("hello")),
        new("nexus-standalone-echo-op")
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    logger.LogInformation("Started Echo operation OperationID {OperationId}", echoHandle.Id);

    var echoResult = await echoHandle.GetResultAsync();
    logger.LogInformation("Echo result: {Message}", echoResult.Message);

    // Execute the async (workflow-backed) Hello operation.
    var helloHandle = await nexusClient.StartNexusOperationAsync(
        svc => svc.SayHello(new("Temporal", IHelloService.HelloLanguage.En)),
        new("nexus-standalone-hello-op")
        {
            ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
        });
    logger.LogInformation("Started Hello operation OperationID {OperationId}", helloHandle.Id);

    var helloResult = await helloHandle.GetResultAsync();
    logger.LogInformation("Hello result: {Message}", helloResult.Message);

    // List Nexus operations using the base client (not the Nexus client).
    logger.LogInformation("ListNexusOperations results:");
    await foreach (var op in client.ListNexusOperationsAsync(
        $"Endpoint = '{IHelloService.EndpointName}'"))
    {
        logger.LogInformation(
            "\tOperationID: {OperationId}, Operation: {Operation}, Status: {Status}",
            op.OperationId,
            op.Operation,
            op.Status);
    }

    // Count Nexus operations using the base client (not the Nexus client).
    var countResp = await client.CountNexusOperationsAsync(
        $"Endpoint = '{IHelloService.EndpointName}'");
    logger.LogInformation("Total Nexus operations: {Count}", countResp.Count);
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
