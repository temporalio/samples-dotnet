using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SafeMessageHandlers;

// Create a client to localhost on default namespace
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = loggerFactory;
var client = await TemporalClient.ConnectAsync(connectOptions);
var logger = loggerFactory.CreateLogger<Program>();

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
    logger.LogInformation("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "atomic-message-handlers-sample").
            AddAllActivities(new ClusterManagerActivities()).
            AddWorkflow<ClusterManagerWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(bool testContinueAsNew)
{
    // Start workflow
    var workflowOptions = new WorkflowOptions(
            id: "atomic-message-handlers-workflow-id",
            taskQueue: "atomic-message-handlers-sample")
    {
        IdConflictPolicy = WorkflowIdConflictPolicy.TerminateExisting,
    };
    workflowOptions.SignalWithStart((ClusterManagerWorkflow wf) => wf.StartClusterAsync());
    var handle = await client.StartWorkflowAsync(
        (ClusterManagerWorkflow wf) => wf.RunAsync(new() { TestContinueAsNew = testContinueAsNew }),
        workflowOptions);

    // Allocate 2 nodes each to 6 jobs
    await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
        handle.ExecuteUpdateAsync(wf => wf.AllocateNodesToJobAsync(
            new(2, $"job-{i}")))));

    // Wait a bit
    await Task.Delay(testContinueAsNew ? 10000 : 1000);

    // Delete the jobs
    await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
        handle.ExecuteUpdateAsync(wf => wf.DeleteJobAsync(new($"job-{i}")))));

    // Shutdown cluster
    await handle.SignalAsync(wf => wf.ShutdownClusterAsync());
    var result = await handle.GetResultAsync();

    logger.LogInformation(
        "Cluster shut down successfully. " +
        "It peaked at {MaxAssignedNodes} assigned nodes. " +
        "It had {NumAssignedNodes} nodes assigned at the end.",
        result.MaxAssignedNodes,
        result.NumAssignedNodes);
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        if (args.Length > 1)
        {
            throw new ArgumentException("No extra options allowed for 'worker'");
        }
        await RunWorkerAsync();
        break;
    case "workflow":
        if (args.Length > 2 || (args.Length == 2 && args[1] != "--test-continue-as-new"))
        {
            throw new ArgumentException("Only '--test-continue-as-new' option allowed for 'worker'");
        }
        await ExecuteWorkflowAsync(args.ElementAtOrDefault(1) == "--test-continue-as-new");
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}