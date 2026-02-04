using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusCancellation.Caller;
using TemporalioSamples.NexusCancellation.Handler;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// Cancellation token cancelled on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

Task<TemporalClient> ConnectClientAsync(string temporalNamespace)
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.Namespace = temporalNamespace;
    connectOptions.LoggerFactory = loggerFactory;
    return TemporalClient.ConnectAsync(connectOptions);
}

async Task RunHandlerWorkerAsync()
{
    // Run worker until cancelled
    logger.LogInformation("Running handler worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync("nexus-cancellation-handler-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-cancellation-handler-sample").
            AddNexusService(new HelloService()).
            AddWorkflow<HelloHandlerWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Handler worker cancelled");
    }
}

async Task RunCallerWorkerAsync()
{
    // Run worker until cancelled
    logger.LogInformation("Running caller worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync("nexus-cancellation-caller-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-cancellation-caller-sample")
            .AddWorkflow<HelloCallerWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Caller worker cancelled");
    }
}

async Task ExecuteCallerWorkflowAsync()
{
    logger.LogInformation("Executing caller hello workflow");

    var client = await ConnectClientAsync("nexus-cancellation-caller-namespace");

    var result = await client.ExecuteWorkflowAsync(
        (HelloCallerWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: "nexus-cancellation-hello-id", taskQueue: "nexus-cancellation-caller-sample"));
    logger.LogInformation("Workflow result: {Result}", result);
}

switch (args.ElementAtOrDefault(0))
{
    case "handler-worker":
        await RunHandlerWorkerAsync();
        break;
    case "caller-worker":
        await RunCallerWorkerAsync();
        break;
    case "caller-workflow":
        await ExecuteCallerWorkflowAsync();
        break;
    default:
        throw new ArgumentException(
            "Must pass 'handler-worker', 'caller-worker', or 'caller-workflow' as the single argument");
}