using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Gcp.CloudRun.Id;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun;

// Connect from environment config (TEMPORAL_ADDRESS, TEMPORAL_NAMESPACE, TEMPORAL_API_KEY, ...).
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));

var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker";

// @@@SNIPSTART dotnet-cloud-run
// The Cloud Run Id plugin sets the client identity to "{instanceId}@{revision}" from Cloud Run
// metadata at connect time; every Worker created from the client inherits it.
connectOptions.Plugins = new ITemporalClientPlugin[] { new CloudRunIdPlugin() };

// ApplyGoogleCloudRunOpenTelemetryDefaults adds the tracing interceptor and a runtime exporting Core
// metrics + traces over OTLP to the collector sidecar; the returned handle owns the tracer provider.
// Both plugins configure the same connect options.
using var telemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults();

var client = await TemporalClient.ConnectAsync(connectOptions);
// @@@SNIPEND
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

// Cloud Run sends SIGTERM ~10s before SIGKILL; defer the default termination so the worker can drain.
using var sigterm = PosixSignalRegistration.Create(
    PosixSignal.SIGTERM,
    ctx =>
    {
        ctx.Cancel = true;
        cts.Cancel();
    });

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(taskQueue)
    {
        GracefulShutdownTimeout = TimeSpan.FromSeconds(5),
    }.
        AddWorkflow<GreetingWorkflow>().
        AddActivity(GreetingActivities.SayHello));

Console.WriteLine(
    "Worker running: taskQueue={0} address={1} namespace={2}",
    taskQueue,
    connectOptions.TargetHost,
    connectOptions.Namespace ?? "default");
try
{
    await worker.ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker shutting down");
}

// Flush traces within the shutdown grace window (Core metrics export periodically, no explicit flush).
await telemetry.FlushAsync(TimeSpan.FromSeconds(2));
Console.WriteLine("Worker stopped");
