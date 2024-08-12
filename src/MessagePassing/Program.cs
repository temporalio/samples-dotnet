using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.MessagePassing;

// Create a client to localhost on default namespace
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = loggerFactory,
});
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
        new TemporalWorkerOptions(taskQueue: "message-passing-sample").
            AddWorkflow<GreetingWorkflow>());
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
    // Start workflow
    var workflowHandle = await client.StartWorkflowAsync(
        (GreetingWorkflow wf) => wf.RunAsync(),
        new(id: "message-passing-workflow-id", taskQueue: "message-passing-sample"));

    logger.LogInformation(
        "Supported languages: {Languages}",
        await workflowHandle.QueryAsync(wf => wf.GetLanguages(new(false))));

    var previousLanguage = await workflowHandle.ExecuteUpdateAsync(
        wf => wf.SetCurrentLanguageAsync(GreetingWorkflow.Language.Chinese));
    var currentLanguage = await workflowHandle.QueryAsync(wf => wf.CurrentLanguage);
    logger.LogInformation(
        "Languages changed: {PreviousLanguage} -> {CurrentLanguage}",
        previousLanguage,
        currentLanguage);

    var updateHandle = await workflowHandle.StartUpdateAsync(
        wf => wf.SetCurrentLanguageAsync(GreetingWorkflow.Language.English),
        new(WorkflowUpdateStage.Accepted));
    previousLanguage = await updateHandle.GetResultAsync();
    currentLanguage = await workflowHandle.QueryAsync(wf => wf.CurrentLanguage);
    logger.LogInformation(
        "Languages changed: {PreviousLanguage} -> {CurrentLanguage}",
        previousLanguage,
        currentLanguage);

    await workflowHandle.SignalAsync(wf => wf.ApproveAsync(new("MyUser")));
    logger.LogInformation("Result: {Result}", await workflowHandle.GetResultAsync());
}

if (args.Length > 1)
{
    throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
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