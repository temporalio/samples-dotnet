using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SearchAttributes;

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

const string TaskQueue = "search-attributes";

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).
            AddAllActivities(new SearchAttributesActivities(client)).
            AddWorkflow<SearchAttributesWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Executing workflow");

    // Start the workflow with an initial value for CustomIntField. The workflow reads it, updates
    // the CustomIntField, upserts more search attributes, unsets a value, and then queries the
    // visibility store for itself.
    var handle = await client.StartWorkflowAsync(
        (SearchAttributesWorkflow wf) => wf.RunAsync(),
        new(id: $"search-attributes-{Guid.NewGuid()}", taskQueue: TaskQueue)
        {
            TypedSearchAttributes = new SearchAttributeCollection.Builder().
                Set(SearchAttributesWorkflow.CustomIntField, 1).
                ToSearchAttributeCollection(),
        });
    await handle.GetResultAsync();
    Console.WriteLine("Workflow completed, see the worker log for search attribute values");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Search attributes sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
