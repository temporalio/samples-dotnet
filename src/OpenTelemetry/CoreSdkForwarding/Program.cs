using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.OpenTelemetry;
using Temporalio.Runtime;
using Temporalio.Worker;
using TemporalioSamples.OpenTelemetry.Common;

var assemblyName = typeof(TemporalClient).Assembly.GetName();

using var meter = new Meter(assemblyName.Name!, assemblyName.Version!.ToString());

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
    Interceptors = new[] { new TracingInterceptor() },
    Runtime = new TemporalRuntime(new TemporalRuntimeOptions()
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
        },
    }),
});

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "opentelemetry-sample-core-sdk-forwarding").
            AddWorkflow<MyWorkflow>().
            AddActivity(Activities.MyActivity));
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
    Console.WriteLine("Executing workflow");
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "opentelemetry-sample-core-sdk-workflow-id", taskQueue: "opentelemetry-sample-core-sdk-forwarding"));
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}