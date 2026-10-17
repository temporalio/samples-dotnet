using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using TemporalioSamples.LambdaWorker.Worker;

async Task<TemporalClient> ConnectClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();
    Console.WriteLine("Connected to Temporal Service");

    var workflowId = $"{LambdaWorkerSample.WorkflowId}-{Guid.NewGuid()}";
    var handle = await client.StartWorkflowAsync(
        (SampleWorkflow wf) => wf.RunAsync("Serverless Lambda Worker!"),
        new(
            id: workflowId,
            taskQueue: LambdaWorkerSample.TaskQueue));

    Console.WriteLine($"Started Workflow ID: {handle.Id}");
    Console.WriteLine($"Started Run ID: {handle.ResultRunId}");

    var result = await handle.GetResultAsync();
    Console.WriteLine($"Workflow result: {result}");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Lambda worker starter sample") { workflowCommand };
rootCommand.SetAction(ExecuteWorkflowAsync);
return await rootCommand.Parse(args).InvokeAsync();
