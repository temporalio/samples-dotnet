using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Worker;
using TemporalioSamples.CloudRunWorker;

// Build client connection options from environment configuration (TEMPORAL_ADDRESS,
// TEMPORAL_NAMESPACE, TEMPORAL_API_KEY, ...). With no API key and no TLS block this connects in
// plaintext, which is what a local dev server (reached over an ngrok TCP tunnel) needs.
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";

// Send all Temporal logs to stdout so Cloud Run captures them in Cloud Logging.
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));

var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "cloud-run-worker";

// The --starter mode runs a single workflow (useful for kicking off work locally against the same
// server the deployed worker polls). Applying the defaults here too propagates a trace context into
// the workflow so the deployed worker's spans join the same distributed trace.
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

// Apply the Google Cloud Run OpenTelemetry defaults: adds the tracing interceptor and configures a
// Temporal runtime that exports Core metrics + traces over OTLP to the local collector sidecar. The
// returned handle owns the tracer provider and is flushed on shutdown.
using var telemetry = connectOptions.ApplyGoogleCloudRunOpenTelemetryDefaults();

var client = await TemporalClient.ConnectAsync(connectOptions);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

// Cloud Run signals shutdown with SIGTERM (about 10 seconds before SIGKILL).
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

// Flush buffered traces within the Cloud Run shutdown grace window. Core metrics are exported
// periodically by the runtime and have no explicit flush.
await telemetry.FlushAsync(TimeSpan.FromSeconds(2));
Console.WriteLine("Worker stopped");
