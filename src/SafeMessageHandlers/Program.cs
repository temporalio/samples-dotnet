using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.SafeMessageHandlers;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

async Task<TemporalClient> ConnectClientAsync()
{
    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = loggerFactory;
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    logger.LogInformation("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "atomic-message-handlers-sample").
            AddAllActivities(new ClusterManagerActivities()).
            AddWorkflow<ClusterManagerWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

var testContinueAsNewOption = new Option<bool>("--test-continue-as-new")
{
    Description = "Use a low history threshold to demonstrate continue-as-new",
};

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var testContinueAsNew = parseResult.GetValue(testContinueAsNewOption);
    var client = await ConnectClientAsync();

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
    await Task.Delay(testContinueAsNew ? 10000 : 1000, cancellationToken);

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

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow") { testContinueAsNewOption };
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Safe message handlers sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
