using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Gcp.CloudRun.Id;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun;

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
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
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker shutting down");
    }

    // Flush traces within the shutdown grace window (Core metrics export periodically, no explicit flush).
    // The action's token is already cancelled by now, so it can't bound the flush.
    await telemetry.FlushAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
    Console.WriteLine("Worker stopped");
}

// Cancellation token passed to the action is cancelled on ctrl+c and SIGTERM
var rootCommand = new RootCommand("Google Cloud Run worker sample");
rootCommand.SetAction(RunWorkerAsync);

// Cloud Run sends SIGTERM ~10s before SIGKILL; the default 2s would abandon the worker drain and trace
// flush above.
var invocationConfig = new InvocationConfiguration { ProcessTerminationTimeout = TimeSpan.FromSeconds(9) };
return await rootCommand.Parse(args).InvokeAsync(invocationConfig);
