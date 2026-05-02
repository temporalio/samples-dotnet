using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ToolRegistryIncidentTriage;

var address = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? throw new InvalidOperationException("TEMPORAL_ADDRESS missing");
var ns = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? throw new InvalidOperationException("TEMPORAL_NAMESPACE missing");
var apiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY") ?? throw new InvalidOperationException("TEMPORAL_API_KEY missing");
var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "triage-dotnet";

Console.WriteLine($"connecting to {address} (ns={ns}) on task queue {taskQueue}");

var client = await TemporalClient.ConnectAsync(new()
{
    TargetHost = address,
    Namespace = ns,
    ApiKey = apiKey,
    Tls = new() { },
});

using var worker = new TemporalWorker(client, new TemporalWorkerOptions(taskQueue)
    .AddWorkflow<IncidentTriageWorkflow>()
    .AddWorkflow<ApprovalWorkflow>()
    .AddAllActivities(new TriageActivity()));

Console.WriteLine($"worker ready — polling {taskQueue}");
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
await worker.ExecuteAsync(cts.Token);
