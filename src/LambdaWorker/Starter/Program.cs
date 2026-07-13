using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using TemporalioSamples.LambdaWorker;

if (args.Length > 0 && args[0] != "workflow")
{
    Console.WriteLine("Usage: dotnet run [workflow]");
    return;
}

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);
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
