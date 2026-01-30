using Microsoft.Extensions.Logging;
using RoutedVersioning;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);
var taskQueue = "rv";
async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    var workerOptions = new TemporalWorkerOptions(taskQueue: taskQueue)
        .AddWorkflow<MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors>()
        .AddActivity(Activities.GenericActivity);
    // Run worker until cancelled
    Console.WriteLine("Running worker");
    Console.WriteLine("MyWorkflow is currently at version {0}", MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors.LatestVersion.Value);
    using var worker = new TemporalWorker(client, workerOptions);

    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task RunStarterAsync()
{
    var workflowId = args.ElementAtOrDefault(2);
    if (workflowId is null)
    {
        throw new ArgumentException("Workflow id is required");
    }

    switch (args.ElementAtOrDefault(1))
    {
        case "--start-workflow":
            {
                var args = new StartMyWorkflowRequest
                {
                    Value = Guid.NewGuid().ToString(),
                    Options = new StartMyWorkflowRequest.ExecutionOptions
                    {
                        Version = MyWorkflowDontChangeThisFileOftenAndDelegateToImplementors.LatestVersion,
                    },
                };
                // Since it's just used for typing purposes, it doesn't matter which one we start
                var handle = await client.StartWorkflowAsync((IMyWorkflow wf) => wf.RunAsync(args), new(id: workflowId, taskQueue: taskQueue));
                Console.WriteLine($"Started workflow with ID {handle.Id} and run ID {handle.ResultRunId}");
                break;
            }
        case "--query-workflow":
            {
                // Since it's just used for typing purposes, it doesn't matter which one we query
                var handle = client.GetWorkflowHandle(workflowId);
                var result = await handle.QueryAsync((IMyWorkflow wf) => wf.GetResult());
                Console.WriteLine($"Query result for ID {handle.Id}: {result}");
                break;
            }
        default:
            throw new ArgumentException("Either --start-workflow or --query-workflow is required");
    }
}
switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "starter":
        await RunStarterAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'starter' as the single argument");
}