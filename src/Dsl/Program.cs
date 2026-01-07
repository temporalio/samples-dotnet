using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Dsl;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
if (string.IsNullOrEmpty(connectOptions.TargetHost))
{
    connectOptions.TargetHost = "localhost:7233";
}

connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

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
        new TemporalWorkerOptions(taskQueue: "dsl-sample")
            .AddAllActivities(typeof(DslActivities), null)
            .AddWorkflow<DslWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(string yamlFile)
{
    var yamlContent = await File.ReadAllTextAsync(yamlFile);
    var dslInput = DslInput.Parse(yamlContent);

    Console.WriteLine($"Executing workflow from {yamlFile}");
    var result = await client.ExecuteWorkflowAsync(
        (DslWorkflow wf) => wf.RunAsync(dslInput),
        new(id: $"dsl-workflow-{Guid.NewGuid()}", taskQueue: "dsl-sample"));

    Console.WriteLine("Workflow completed. Final variables:");
    foreach (var kvp in result)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
    }
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        var yamlFile = args.ElementAtOrDefault(1)
            ?? throw new ArgumentException("Must provide YAML file path as second argument");
        await ExecuteWorkflowAsync(yamlFile);
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the first argument");
}
