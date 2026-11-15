using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Dsl;

async Task<TemporalClient> ConnectClientAsync()
{
    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";

    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "dsl-sample")
            .AddAllActivities(typeof(DslActivities), null)
            .AddWorkflow<DslWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

var yamlFileArgument = new Argument<string>("yaml-file") { Description = "Path to the workflow YAML file" };

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var yamlFile = parseResult.GetRequiredValue(yamlFileArgument);
    var yamlContent = await File.ReadAllTextAsync(yamlFile, cancellationToken);
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

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow from a YAML file") { yamlFileArgument };
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("DSL sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
