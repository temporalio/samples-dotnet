using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.CallerPattern.Caller;
using TemporalioSamples.NexusMessaging.CallerPattern.Handler;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.OnDemandPattern.Caller;
using CallerGreetingWorkflow = TemporalioSamples.NexusMessaging.CallerPattern.Handler.GreetingWorkflow;
using OnDemandGreetingWorkflow = TemporalioSamples.NexusMessaging.OnDemandPattern.Handler.GreetingWorkflow;
using OnDemandNexusService = TemporalioSamples.NexusMessaging.OnDemandPattern.Handler.NexusRemoteGreetingService;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

const string HandlerNamespace = "nexus-messaging-handler-namespace";
const string CallerNamespace = "nexus-messaging-caller-namespace";
const string HandlerTaskQueue = "nexus-messaging-handler-sample";
const string CallerTaskQueue = "nexus-messaging-caller-sample";

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
    logger.LogInformation("Running handler worker");
    var client = await ConnectClientAsync(HandlerNamespace);

    // Start entity workflow with UseExisting policy (entity pattern: pre-start at boot)
    var workflowId = "GreetingWorkflow_for_default-user";
    try
    {
        await client.StartWorkflowAsync(
            (CallerGreetingWorkflow wf) => wf.RunAsync("default-user"),
            new(id: workflowId, taskQueue: HandlerTaskQueue)
            {
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
            });
        logger.LogInformation("Started entity workflow {WorkflowId}", workflowId);
    }
    catch (Temporalio.Exceptions.RpcException ex)
    {
        logger.LogWarning(ex, "Could not start entity workflow");
    }

    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(HandlerTaskQueue).
            AddNexusService(new NexusGreetingService()).
            AddWorkflow<CallerGreetingWorkflow>().
            AddAllActivities(new GreetingActivities()));
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
    logger.LogInformation("Running caller worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(CallerNamespace),
        new TemporalWorkerOptions(CallerTaskQueue).
            AddWorkflow<CallerWorkflow>());
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
    logger.LogInformation("Executing caller workflow");
    var client = await ConnectClientAsync(CallerNamespace);
    var result = await client.ExecuteWorkflowAsync(
        (CallerWorkflow wf) => wf.RunAsync("default-user"),
        new(id: "nexus-messaging-caller-workflow-id", taskQueue: CallerTaskQueue));
    logger.LogInformation("Caller workflow result:");
    foreach (var entry in result)
    {
        logger.LogInformation("  {Entry}", entry);
    }
}

async Task RunRemoteHandlerWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    logger.LogInformation("Running remote handler worker (on-demand pattern)");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(HandlerNamespace),
        new TemporalWorkerOptions(HandlerTaskQueue).
            AddNexusService(new OnDemandNexusService()).
            AddWorkflow<OnDemandGreetingWorkflow>().
            AddAllActivities(new GreetingActivities()));
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Remote handler worker cancelled");
    }
}

async Task RunRemoteCallerWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    logger.LogInformation("Running remote caller worker (on-demand pattern)");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(CallerNamespace),
        new TemporalWorkerOptions(CallerTaskQueue).
            AddWorkflow<CallerRemoteWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Remote caller worker cancelled");
    }
}

async Task ExecuteRemoteCallerWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    logger.LogInformation("Executing remote caller workflow (on-demand pattern)");
    var client = await ConnectClientAsync(CallerNamespace);
    var result = await client.ExecuteWorkflowAsync(
        (CallerRemoteWorkflow wf) => wf.RunAsync(),
        new(id: "nexus-messaging-remote-caller-workflow-id", taskQueue: CallerTaskQueue));
    logger.LogInformation("Remote caller workflow result:");
    foreach (var entry in result)
    {
        logger.LogInformation("  {Entry}", entry);
    }
}

// Cancellation token passed to actions is cancelled on ctrl+c
var handlerWorkerCommand = new Command("handler-worker", "Run handler worker (caller pattern)");
handlerWorkerCommand.SetAction(RunHandlerWorkerAsync);

var callerWorkerCommand = new Command("caller-worker", "Run caller worker (caller pattern)");
callerWorkerCommand.SetAction(RunCallerWorkerAsync);

var callerWorkflowCommand = new Command("caller-workflow", "Execute caller workflow (caller pattern)");
callerWorkflowCommand.SetAction(ExecuteCallerWorkflowAsync);

var remoteHandlerWorkerCommand = new Command("remote-handler-worker", "Run remote handler worker (on-demand pattern)");
remoteHandlerWorkerCommand.SetAction(RunRemoteHandlerWorkerAsync);

var remoteCallerWorkerCommand = new Command("remote-caller-worker", "Run remote caller worker (on-demand pattern)");
remoteCallerWorkerCommand.SetAction(RunRemoteCallerWorkerAsync);

var remoteCallerWorkflowCommand = new Command("remote-caller-workflow", "Execute remote caller workflow (on-demand pattern)");
remoteCallerWorkflowCommand.SetAction(ExecuteRemoteCallerWorkflowAsync);

var rootCommand = new RootCommand("Nexus messaging sample")
{
    handlerWorkerCommand,
    callerWorkerCommand,
    callerWorkflowCommand,
    remoteHandlerWorkerCommand,
    remoteCallerWorkerCommand,
    remoteCallerWorkflowCommand,
};
return await rootCommand.Parse(args).InvokeAsync();
