using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.ContextPropagation;

using var loggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
if (string.IsNullOrEmpty(connectOptions.TargetHost))
{
    connectOptions.TargetHost = "localhost:7233";
}
connectOptions.LoggerFactory = loggerFactory;
// This is where we set the interceptor to propagate context
connectOptions.Interceptors = new[]
{
    new ContextPropagationInterceptor<string?>(
        MyContext.UserIdLocal,
        DataConverter.Default.PayloadConverter),
};
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

    // Run worker until cancelled
    logger.LogInformation("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "interceptors-sample").
            AddAllActivities<SayHelloActivities>(new()).
            AddWorkflow<SayHelloWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
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

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}