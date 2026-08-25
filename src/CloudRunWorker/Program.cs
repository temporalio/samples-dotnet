using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Worker;
using TemporalioSamples.CloudRunWorker;

// Cloud Run injects these via `--set-env-vars`; fall back to a local dev server for convenience.
var address = GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? "localhost:7233";
var temporalNamespace = GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default";
var taskQueue = GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker-sample";

using var loggerFactory = LoggerFactory.Create(builder => builder.
    AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
    SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("CloudRunWorker");

var clientOptions = new TemporalClientConnectOptions(address)
{
    Namespace = temporalNamespace,
    LoggerFactory = loggerFactory,
};

// Reads the Cloud Run instance id from the metadata server, and the worker pool / service name and
// revision from the environment. This also sets the client Identity to "{instanceId}@{revision}"
// (unless one was already configured). The returned metadata is reused for the worker below.
//
// NOTE: this requires the process to be running on a Cloud Run worker pool or service. Running it
// elsewhere throws because the metadata server is unreachable.
var metadata = await clientOptions.ApplyGoogleCloudRunDefaultsAsync();
logger.LogInformation(
    "Resolved Cloud Run worker identity {Identity} (name={Name}, revision={Revision})",
    metadata.WorkerIdentity,
    metadata.Name,
    metadata.Revision);

var client = await TemporalClient.ConnectAsync(clientOptions);

var workerOptions = new TemporalWorkerOptions(taskQueue).
    AddWorkflow<SampleWorkflow>().
    AddActivity(Activities.SayHello);

// Enables worker versioning using the Cloud Run deployment version (deployment name = worker pool /
// service name, build id = revision) and pins workflows to this version by default.
workerOptions.ApplyGoogleCloudRunDefaults(metadata);

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
