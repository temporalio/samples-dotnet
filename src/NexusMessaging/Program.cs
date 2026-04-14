using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusMessaging.Callerpattern;
using TemporalioSamples.NexusMessaging.Callerpattern.Caller;
using TemporalioSamples.NexusMessaging.Callerpattern.Handler;
using TemporalioSamples.NexusMessaging.Common;
using TemporalioSamples.NexusMessaging.Ondemandpattern;
using TemporalioSamples.NexusMessaging.Ondemandpattern.Caller;
using CallerGreetingWorkflow = TemporalioSamples.NexusMessaging.Callerpattern.Handler.GreetingWorkflow;
using OndemandGreetingWorkflow = TemporalioSamples.NexusMessaging.Ondemandpattern.Handler.GreetingWorkflow;
using OndemandNexusService = TemporalioSamples.NexusMessaging.Ondemandpattern.Handler.NexusRemoteGreetingService;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// Cancellation token cancelled on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

const string HandlerNamespace = "nexus-messaging-handler-namespace";
const string CallerNamespace = "nexus-messaging-caller-namespace";
const string HandlerTaskQueue = "nexus-messaging-handler-task-queue";
const string CallerTaskQueue = "nexus-messaging-caller-task-queue";

Task<TemporalClient> ConnectClientAsync(string temporalNamespace)
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.Namespace = temporalNamespace;
    connectOptions.LoggerFactory = loggerFactory;
    return TemporalClient.ConnectAsync(connectOptions);
}

async Task RunHandlerWorkerAsync()
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
        logger.LogWarning(ex, "Could not start entity workflow, may already exist");
    }

    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(HandlerTaskQueue).
            AddNexusService(new NexusGreetingService()).
            AddWorkflow<CallerGreetingWorkflow>().
            AddAllActivities(new GreetingActivities()));
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Handler worker cancelled");
    }
}

async Task RunCallerWorkerAsync()
{
    logger.LogInformation("Running caller worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(CallerNamespace),
        new TemporalWorkerOptions(CallerTaskQueue).
            AddWorkflow<CallerWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Caller worker cancelled");
    }
}

async Task ExecuteCallerWorkflowAsync()
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

async Task RunRemoteHandlerWorkerAsync()
{
    logger.LogInformation("Running remote handler worker (on-demand pattern)");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(HandlerNamespace),
        new TemporalWorkerOptions(HandlerTaskQueue).
            AddNexusService(new OndemandNexusService()).
            AddWorkflow<OndemandGreetingWorkflow>().
            AddAllActivities(new GreetingActivities()));
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Remote handler worker cancelled");
    }
}

async Task RunRemoteCallerWorkerAsync()
{
    logger.LogInformation("Running remote caller worker (on-demand pattern)");
    using var worker = new TemporalWorker(
        await ConnectClientAsync(CallerNamespace),
        new TemporalWorkerOptions(CallerTaskQueue).
            AddWorkflow<CallerRemoteWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Remote caller worker cancelled");
    }
}

async Task ExecuteRemoteCallerWorkflowAsync()
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

switch (args.ElementAtOrDefault(0))
{
    case "handler-worker":
        await RunHandlerWorkerAsync();
        break;
    case "caller-worker":
        await RunCallerWorkerAsync();
        break;
    case "caller-workflow":
        await ExecuteCallerWorkflowAsync();
        break;
    case "remote-handler-worker":
        await RunRemoteHandlerWorkerAsync();
        break;
    case "remote-caller-worker":
        await RunRemoteCallerWorkerAsync();
        break;
    case "remote-caller-workflow":
        await ExecuteRemoteCallerWorkflowAsync();
        break;
    default:
        throw new ArgumentException(
            "Must pass 'handler-worker', 'caller-worker', 'caller-workflow', " +
            "'remote-handler-worker', 'remote-caller-worker', or 'remote-caller-workflow' as the single argument");
}
