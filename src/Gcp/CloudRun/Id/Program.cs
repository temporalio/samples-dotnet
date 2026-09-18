using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun.Id;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun.Id;

// @@@SNIPSTART dotnet-cloud-run-id
var address = GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var temporalNamespace = GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";
var taskQueue = GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker-sample";

using var loggerFactory = LoggerFactory.Create(builder => builder.
    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
    SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("CloudRunId");

// Derives the client identity "{instanceId}@{revision}" from Cloud Run metadata at connect time.
var clientOptions = new TemporalClientConnectOptions(address)
{
    Namespace = temporalNamespace,
    LoggerFactory = loggerFactory,
    Plugins = new[] { new CloudRunIdPlugin() },
};

var client = await TemporalClient.ConnectAsync(clientOptions);
// @@@SNIPEND
var metadata = await GoogleCloudRunMetadata.FetchAsync();
logger.LogInformation("Cloud Run identity: {Identity}", metadata.Identity);

var workerOptions = new TemporalWorkerOptions(taskQueue).
    AddWorkflow<SampleWorkflow>().
    AddActivity(Activities.SayHello);

using var worker = new TemporalWorker(client, workerOptions);

// Cloud Run sends SIGTERM before stopping an instance; shut down gracefully on that and Ctrl+C.
using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};
using var sigterm = PosixSignalRegistration.Create(
    PosixSignal.SIGTERM,
    context =>
    {
        context.Cancel = true;
        cancellationSource.Cancel();
    });

logger.LogInformation(
    "Worker started on task queue '{TaskQueue}' against {Address} (namespace '{Namespace}').",
    taskQueue,
    address,
    temporalNamespace);
try
{
    await worker.ExecuteAsync(cancellationSource.Token);
}
catch (OperationCanceledException)
{
    logger.LogInformation("Shutdown signal received; worker stopped.");
}

static string? GetEnvironmentVariable(string name) =>
    Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
        ? value
        : null;
