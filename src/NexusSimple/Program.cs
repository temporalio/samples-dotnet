using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusSimple;
using TemporalioSamples.NexusSimple.Caller;
using TemporalioSamples.NexusSimple.Handler;

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
        await ConnectClientAsync("nexus-simple-handler-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-simple-handler-sample").
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
        await ConnectClientAsync("nexus-simple-caller-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-simple-caller-sample").
            AddWorkflow<EchoCallerWorkflow>().
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
    logger.LogInformation("Executing caller echo workflow");
    var client = await ConnectClientAsync("nexus-simple-caller-namespace");
    var result1 = await client.ExecuteWorkflowAsync(
        (EchoCallerWorkflow wf) => wf.RunAsync("Nexus Echo 👋"),
        new(id: "nexus-simple-echo-id", taskQueue: "nexus-simple-caller-sample"));
    logger.LogInformation("Workflow result: {Result}", result1);

    logger.LogInformation("Executing caller hello workflow");
    var result2 = await client.ExecuteWorkflowAsync(
        (HelloCallerWorkflow wf) => wf.RunAsync("Temporal", IHelloService.HelloLanguage.Es),
        new(id: "nexus-simple-hello-id", taskQueue: "nexus-simple-caller-sample"));
    logger.LogInformation("Workflow result: {Result}", result2);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var handlerWorkerCommand = new Command("handler-worker", "Run handler worker");
handlerWorkerCommand.SetAction(RunHandlerWorkerAsync);

var callerWorkerCommand = new Command("caller-worker", "Run caller worker");
callerWorkerCommand.SetAction(RunCallerWorkerAsync);

var callerWorkflowCommand = new Command("caller-workflow", "Execute caller workflow");
callerWorkflowCommand.SetAction(ExecuteCallerWorkflowAsync);

var rootCommand = new RootCommand("Nexus simple sample") { handlerWorkerCommand, callerWorkerCommand, callerWorkflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
