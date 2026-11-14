using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;

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
    // This is where we set the interceptor to propagate context
    connectOptions.Interceptors = new[]
    {
        new ContextPropagationInterceptor<string?>(
            MyContext.UserIdLocal,
            DataConverter.Default.PayloadConverter),
    };
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    logger.LogInformation("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "interceptors-sample").
            AddAllActivities<SayHelloActivities>(new()).
            AddWorkflow<SayHelloWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Set our user ID that can be accessed in the workflow and activity
    MyContext.UserId = "some-user";

    // Start workflow, send signal, wait for completion, issue query
    logger.LogInformation("Executing workflow");
    var handle = await client.StartWorkflowAsync(
        (SayHelloWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: "interceptors-workflow-id", taskQueue: "interceptors-sample"));
    await handle.SignalAsync(wf => wf.SignalCompleteAsync());
    var result = await handle.GetResultAsync();
    _ = await handle.QueryAsync(wf => wf.IsComplete());
    logger.LogInformation("Workflow result: {Result}", result);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Context propagation sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
