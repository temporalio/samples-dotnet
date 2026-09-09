using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun.WorkerId;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun.WorkerId;

// Cloud Run injects these via `--set-env-vars`; fall back to a local dev server for convenience.
var address = GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var temporalNamespace = GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";
var taskQueue = GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker-sample";

using var loggerFactory = LoggerFactory.Create(builder => builder.
    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
    SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("CloudRunWorkerId");

// Register the Cloud Run plugin once on the client. At connect time it reads the Cloud Run instance
// id from the metadata server, and the worker pool / service name and revision from the environment,
// then sets the client Identity to the worker identity "{instanceId}@{revision}" (unless one was
// already configured). Every worker created from this client inherits that identity. The plugin only
// sets the worker identity; it does not configure anything else.
//
// NOTE: this requires the process to be running on a Cloud Run worker pool or service. Running it
// elsewhere throws at connect time because the metadata server is unreachable.
var clientOptions = new TemporalClientConnectOptions(address)
{
    Namespace = temporalNamespace,
    LoggerFactory = loggerFactory,
    Plugins = new[] { new WorkerIdPlugin() },
};

var client = await TemporalClient.ConnectAsync(clientOptions);

// The plugin already applied this identity to the client above; read the metadata directly to log
// the worker identity this process runs under.
var metadata = await GoogleCloudRunMetadata.FetchAsync();
logger.LogInformation("Cloud Run worker identity: {WorkerIdentity}", metadata.WorkerIdentity);

var workerOptions = new TemporalWorkerOptions(taskQueue).
    AddWorkflow<SampleWorkflow>().
    AddActivity(Activities.SayHello);

using var worker = new TemporalWorker(client, workerOptions);

// Cloud Run sends SIGTERM before stopping an instance; shut the worker down gracefully on that and
// on Ctrl+C so in-flight tasks can finish.
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
