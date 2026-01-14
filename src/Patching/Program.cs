using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Patching;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
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

    var workerOptions = new TemporalWorkerOptions(taskQueue: "patching-task-queue")
        .AddActivity(Activities.PrePatchActivity)
        .AddActivity(Activities.PostPatchActivity);

    switch (args.ElementAtOrDefault(2))
    {
        case "initial":
            workerOptions.AddWorkflow<MyWorkflow1Initial>();
            break;
        case "patched":
            workerOptions.AddWorkflow<MyWorkflow2Patched>();
            break;
        case "patch-deprecated":
            workerOptions.AddWorkflow<MyWorkflow3PatchDeprecated>();
            break;
        case "patch-complete":
            workerOptions.AddWorkflow<MyWorkflow4PatchComplete>();
            break;
        default:
            throw new ArgumentException("Which workflow. Can be 'initial', 'patched', 'patch-deprecated', or 'patch-complete'");
    }

    // Run worker until cancelled
    Console.WriteLine("Running worker");

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
                // Since it's just used for typing purposes, it doesn't matter which one we start
                var handle = await client.StartWorkflowAsync((MyWorkflow1Initial wf) => wf.RunAsync(), new(id: workflowId, taskQueue: "patching-task-queue"));
                Console.WriteLine($"Started workflow with ID {handle.Id} and run ID {handle.ResultRunId}");
                break;
            }
        case "--query-workflow":
            {
                // Since it's just used for typing purposes, it doesn't matter which one we query
                var handle = client.GetWorkflowHandle(workflowId);
                var result = await handle.QueryAsync((MyWorkflow1Initial wf) => wf.Result);
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