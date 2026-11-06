using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Common.EnvConfig;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;
using TemporalioSamples.NexusContextPropagation;
using TemporalioSamples.NexusContextPropagation.Caller;
using TemporalioSamples.NexusContextPropagation.Handler;

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
    connectOptions.Interceptors = new IClientInterceptor[]
    {
        new ContextPropagationInterceptor<string?>(
            MyContext.UserIdLocal,
            DataConverter.Default.PayloadConverter),
        // Separate interceptor just for moving in and out of Nexus operation headers. This could
        // have been implemented in the ContextPropagationInterceptor, but for sample logic
        // separation, it was added as a separate interceptor in this project instead.
        new NexusContextPropagationInterceptor(MyContext.UserIdLocal),
    };
    return TemporalClient.ConnectAsync(connectOptions);
}

async Task RunHandlerWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // Run worker until cancelled
    logger.LogInformation("Running handler worker");
    using var worker = new TemporalWorker(
        await ConnectClientAsync("nexus-context-propagation-handler-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-context-propagation-handler-sample").
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
        await ConnectClientAsync("nexus-context-propagation-caller-namespace"),
        new TemporalWorkerOptions(taskQueue: "nexus-context-propagation-caller-sample").
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
    // Set our user ID that can be accessed in the workflows and Nexus service
    MyContext.UserId = "some-user";

    logger.LogInformation("Executing caller workflow");
    var client = await ConnectClientAsync("nexus-context-propagation-caller-namespace");
    var result = await client.ExecuteWorkflowAsync(
        (HelloCallerWorkflow wf) => wf.RunAsync("Temporal", IHelloService.HelloLanguage.Es),
        new(id: "nexus-context-propagation-id", taskQueue: "nexus-context-propagation-caller-sample"));
    logger.LogInformation("Workflow result: {Result}", result);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var handlerWorkerCommand = new Command("handler-worker", "Run handler worker");
handlerWorkerCommand.SetAction(RunHandlerWorkerAsync);

var callerWorkerCommand = new Command("caller-worker", "Run caller worker");
callerWorkerCommand.SetAction(RunCallerWorkerAsync);

var callerWorkflowCommand = new Command("caller-workflow", "Execute caller workflow");
callerWorkflowCommand.SetAction(ExecuteCallerWorkflowAsync);

var rootCommand = new RootCommand("Nexus context propagation sample") { handlerWorkerCommand, callerWorkerCommand, callerWorkflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
