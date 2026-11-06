using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.Patching;

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

var workflowOption = new Option<string>("--workflow")
{
    Description = "Which workflow version the worker runs",
    Required = true,
};
workflowOption.AcceptOnlyFromAmong("initial", "patched", "patch-deprecated", "patch-complete");

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var workerOptions = new TemporalWorkerOptions(taskQueue: "patching-task-queue")
        .AddActivity(Activities.PrePatchActivity)
        .AddActivity(Activities.PostPatchActivity);

    switch (parseResult.GetRequiredValue(workflowOption))
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
    }

    // Run worker until cancelled
    Console.WriteLine("Running worker");

    using var worker = new TemporalWorker(client, workerOptions);

    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

var startWorkflowOption = new Option<string>("--start-workflow") { Description = "Start a workflow with this id" };
var queryWorkflowOption = new Option<string>("--query-workflow") { Description = "Query the workflow with this id" };

async Task RunStarterAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    if (parseResult.GetValue(startWorkflowOption) is { } startWorkflowId)
    {
        // Since it's just used for typing purposes, it doesn't matter which one we start
        var handle = await client.StartWorkflowAsync((MyWorkflow1Initial wf) => wf.RunAsync(), new(id: startWorkflowId, taskQueue: "patching-task-queue"));
        Console.WriteLine($"Started workflow with ID {handle.Id} and run ID {handle.ResultRunId}");
    }
    else
    {
        // Since it's just used for typing purposes, it doesn't matter which one we query
        var handle = client.GetWorkflowHandle(parseResult.GetRequiredValue(queryWorkflowOption));
        var result = await handle.QueryAsync((MyWorkflow1Initial wf) => wf.Result);
        Console.WriteLine($"Query result for ID {handle.Id}: {result}");
    }
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker") { workflowOption };
workerCommand.SetAction(RunWorkerAsync);

var starterCommand = new Command("starter", "Start or query a workflow") { startWorkflowOption, queryWorkflowOption };
starterCommand.Validators.Add(result =>
{
    var hasStart = result.GetResult(startWorkflowOption) is not null;
    var hasQuery = result.GetResult(queryWorkflowOption) is not null;
    if (hasStart == hasQuery)
    {
        result.AddError("Exactly one of --start-workflow or --query-workflow is required");
    }
});
starterCommand.SetAction(RunStarterAsync);

var rootCommand = new RootCommand("Patching sample") { workerCommand, starterCommand };
return await rootCommand.Parse(args).InvokeAsync();
