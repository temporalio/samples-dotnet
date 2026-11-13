using System.CommandLine;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.ExternalStorage;

const string taskQueue = "external-storage-sample";

async Task<TemporalClient> ConnectClientAsync(IAmazonS3 s3Client)
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    // The worker uses its client's data converter, so this is all it takes to offload the large
    // activity inputs and results it sends.
    connectOptions.DataConverter = Storage.CreateDataConverter(s3Client, Storage.Bucket);
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

Task RunS3MockAsync(ParseResult parseResult, CancellationToken cancellationToken) =>
    S3Mock.RunAsync(cancellationToken);

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    using var s3Client = Storage.CreateS3Client();
    var client = await ConnectClientAsync(s3Client);

    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue).
            AddActivity(OrderActivities.FetchOrders).
            AddActivity(OrderActivities.SummarizeOrders).
            AddWorkflow<ProcessOrderBatchWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    using var s3Client = Storage.CreateS3Client();
    var client = await ConnectClientAsync(s3Client);

    var handle = await client.StartWorkflowAsync(
        (ProcessOrderBatchWorkflow wf) => wf.RunAsync(200),
        new(id: "external-storage-workflow-id", taskQueue: taskQueue));
    Console.WriteLine($"Started workflow, run ID: {handle.ResultRunId}");

    var summary = await handle.GetResultAsync();
    Console.WriteLine($"Processed {summary.OrderCount} orders weighing {summary.TotalWeightKg:F1} kg in total");
}

var s3MockCommand = new Command("s3-mock", "Run mock S3 server");
s3MockCommand.SetAction(RunS3MockAsync);

var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("External storage sample") { s3MockCommand, workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
