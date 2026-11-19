using System.CommandLine;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.NexusSerializationContext;

async Task<TemporalClient> ConnectAsync(string temporalNamespace)
{
    var options = ClientEnvConfig.LoadClientConnectOptions();
    options.TargetHost ??= "localhost:7233";
    options.Namespace = temporalNamespace;
    options.DataConverter = temporalNamespace == SampleConfig.Caller ? SampleConfig.CallerConverter() :
        SampleConfig.HandlerConverter(temporalNamespace == SampleConfig.CompressedHandler);
    return await TemporalClient.ConnectAsync(options);
}

async Task RunWorkerAsync(string temporalNamespace, CancellationToken cancellationToken)
{
    var options = new TemporalWorkerOptions(temporalNamespace);
    if (temporalNamespace == SampleConfig.Caller)
    {
        options.AddWorkflow<CallerWorkflow>();
    }
    else
    {
        options.AddWorkflow<EchoWorkflow>().AddNexusService(new SyncEchoService()).AddNexusService(new AsyncEchoService());
    }
    using var worker = new TemporalWorker(await ConnectAsync(temporalNamespace), options);
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker stopped");
    }
}

var compressed = new Command("compressed-handler-worker", "Run compressed/encrypted handler worker");
compressed.SetAction((_, cancellation) => RunWorkerAsync(SampleConfig.CompressedHandler, cancellation));
var encrypted = new Command("encrypted-handler-worker", "Run encrypted handler worker");
encrypted.SetAction((_, cancellation) => RunWorkerAsync(SampleConfig.EncryptedHandler, cancellation));
var caller = new Command("caller-worker", "Run caller worker");
caller.SetAction((_, cancellation) => RunWorkerAsync(SampleConfig.Caller, cancellation));
var start = new Command("caller-workflow", "Run all eight operations");
start.SetAction(async (_, cancellation) =>
{
    var client = await ConnectAsync(SampleConfig.Caller);
    var result = await client.ExecuteWorkflowAsync(
        (CallerWorkflow workflow) => workflow.RunAsync("Hello from Nexus"),
        new($"nexus-serialization-{Guid.NewGuid()}", SampleConfig.Caller));
    foreach (var pair in result)
    {
        Console.WriteLine($"{pair.Endpoint}: sync={pair.Sync}; async={pair.Async}");
    }
});
return await new RootCommand("Nexus serialization context") { compressed, encrypted, caller, start }.Parse(args).InvokeAsync();
