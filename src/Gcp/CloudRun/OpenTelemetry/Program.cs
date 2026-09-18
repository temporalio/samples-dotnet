using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun.OpenTelemetry;

// Connect from environment config (TEMPORAL_ADDRESS, TEMPORAL_NAMESPACE, TEMPORAL_API_KEY, ...).
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));

var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker";

// --starter runs a single workflow, e.g. to kick off work against the same server the worker polls.
if (args.Contains("--starter"))
{
    using var starterTelemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults();
    var starterClient = await TemporalClient.ConnectAsync(connectOptions);
    var greeting = await starterClient.ExecuteWorkflowAsync(
        (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
        new($"cloud-run-worker-{Guid.NewGuid():N}", taskQueue));
    Console.WriteLine("Workflow result: {0}", greeting);
    await starterTelemetry.FlushAsync(TimeSpan.FromSeconds(2));
    return;
}

// Adds the tracing interceptor and a runtime exporting Core metrics + traces over OTLP to the
// collector sidecar; the returned handle owns the tracer provider.
using var telemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults();

var client = await TemporalClient.ConnectAsync(connectOptions);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

// Cloud Run sends SIGTERM ~10s before SIGKILL.
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());

using var worker = new TemporalWorker(
    client, CloudRunWorkerSample.ConfigureOptions(new(taskQueue)));

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
