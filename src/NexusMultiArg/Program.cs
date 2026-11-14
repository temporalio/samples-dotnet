using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusMultiArg;
using TemporalioSamples.NexusMultiArg.Caller;
using TemporalioSamples.NexusMultiArg.Handler;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

Task<TemporalClient> ConnectClientAsync(string temporalNamespace)
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.Namespace = temporalNamespace;
    connectOptions.LoggerFactory = loggerFactory;
    return TemporalClient.ConnectAsync(connectOptions);
}

async Task RunHandlerWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // Run worker until cancelled
    logger.LogInformation("Running handler worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync("nexus-multi-arg-handler-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-multi-arg-handler-sample").
            AddNexusService(new HelloService()).
            AddWorkflow<HelloHandlerWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Handler worker cancelled");
    }
}

async Task RunCallerWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // Run worker until cancelled
    logger.LogInformation("Running caller worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync("nexus-multi-arg-caller-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-multi-arg-caller-sample").
            AddWorkflow<HelloCallerWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Caller worker cancelled");
    }
}

async Task ExecuteCallerWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    logger.LogInformation("Executing caller hello workflow");
    var client = await ConnectClientAsync("nexus-multi-arg-caller-namespace");
    var result = await client.ExecuteWorkflowAsync(
        (HelloCallerWorkflow wf) => wf.RunAsync("Temporal", IHelloService.HelloLanguage.Es),
        new(id: "nexus-multi-arg-id", taskQueue: "nexus-multi-arg-caller-sample"));
    logger.LogInformation("Workflow result: {Result}", result);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var handlerWorkerCommand = new Command("handler-worker", "Run handler worker");
handlerWorkerCommand.SetAction(RunHandlerWorkerAsync);

var callerWorkerCommand = new Command("caller-worker", "Run caller worker");
callerWorkerCommand.SetAction(RunCallerWorkerAsync);

var callerWorkflowCommand = new Command("caller-workflow", "Execute caller workflow");
callerWorkflowCommand.SetAction(ExecuteCallerWorkflowAsync);

var rootCommand = new RootCommand("Nexus multi-arg sample") { handlerWorkerCommand, callerWorkerCommand, callerWorkflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
