using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.EagerWorkflowStart;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

const string TaskQueue = "eager-wf-start-sample";

// Create an activity instance
var activities = new Activities();

// Run worker and start workflow in same process to demonstrate eager workflow start
Console.WriteLine("Running worker and starting workflow with eager start...");
using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(TaskQueue).
        AddActivity(activities.Greeting).
        AddWorkflow<EagerWorkflowStartWorkflow>());

await worker.ExecuteAsync(async () =>
{
    // Start workflow with eager start enabled
    var handle = await client.StartWorkflowAsync(
        (EagerWorkflowStartWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: $"eager-workflow-{Guid.NewGuid()}", taskQueue: TaskQueue)
        {
            RequestEagerStart = true,
        });

    Console.WriteLine($"Started workflow {handle.Id}");

    // Wait for result
    var result = await handle.GetResultAsync();
    Console.WriteLine($"Workflow result: {result}");
});