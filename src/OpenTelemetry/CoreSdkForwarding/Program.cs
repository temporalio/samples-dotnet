using System.CommandLine;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Runtime;
using Temporalio.Worker;
using TemporalioSamples.OpenTelemetry.Common;

async Task RunWorkerAsync(TemporalClient client, CancellationToken cancellationToken)
{
    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "opentelemetry-sample-core-sdk-forwarding").
            AddWorkflow<MyWorkflow>().
            AddActivity(Activities.MyActivity));
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(TemporalClient client)
{
    Console.WriteLine("Executing workflow");
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "opentelemetry-sample-core-sdk-workflow-id", taskQueue: "opentelemetry-sample-core-sdk-forwarding"));
}

async Task RunAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var assemblyName = typeof(TemporalClient).Assembly.GetName();

    // The command name ("worker" or "workflow") is used as the service instance ID
    var instanceId = parseResult.CommandResult.Command.Name;

    var resourceBuilder = ResourceBuilder.
        CreateDefault().
        AddService("TemporalioSamples.OpenTelemetry", serviceInstanceId: instanceId);

    using var tracerProvider = Sdk.
        CreateTracerProviderBuilder().
        SetResourceBuilder(resourceBuilder).
        AddSource(TracingInterceptor.ClientSource.Name, TracingInterceptor.WorkflowsSource.Name, TracingInterceptor.ActivitiesSource.Name).
        AddOtlpExporter().
        Build();

    // Shared by the client and by Core SDK log forwarding below. The OpenTelemetry provider exports
    // logs to the dashboard alongside the traces and metrics.
    using var loggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.AddOtlpExporter();
            }).
            SetMinimumLevel(LogLevel.Information));

    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = loggerFactory;
    connectOptions.Interceptors = new[] { new TracingInterceptor() };
    connectOptions.Runtime = new TemporalRuntime(new TemporalRuntimeOptions()
    {
        Telemetry = new TelemetryOptions()
        {
            Metrics = new MetricsOptions()
            {
                OpenTelemetry = new OpenTelemetryOptions()
                {
                    Url = new Uri("http://localhost:4317"),
                },
            },
            Logging = new LoggingOptions()
            {
                // Core SDK logs default to WARN; lowered here so there is more to see.
                Filter = new TelemetryFilterOptions(core: TelemetryFilterOptions.Level.Info),

                // The Core SDK writes its logs to the console itself unless Forwarding is set, in
                // which case they go to this ILogger instead.
                Forwarding = new LogForwardingOptions(loggerFactory.CreateLogger("Temporalio.Core")),
            },
        },
    });
    var client = await TemporalClient.ConnectAsync(connectOptions);

    if (instanceId == "worker")
    {
        await RunWorkerAsync(client, cancellationToken);
    }
    else
    {
        await ExecuteWorkflowAsync(client);
    }
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(RunAsync);

var rootCommand = new RootCommand("OpenTelemetry Core SDK forwarding sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
