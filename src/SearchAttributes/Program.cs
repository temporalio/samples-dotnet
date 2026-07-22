using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SearchAttributes;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

const string TaskQueue = "search-attributes";

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
        new TemporalWorkerOptions(TaskQueue).
            AddAllActivities(new SearchAttributesActivities(client)).
            AddWorkflow<SearchAttributesWorkflow>());
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
