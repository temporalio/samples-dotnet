using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Converters;
using TemporalioSamples.Encryption.Codec;
using TemporalioSamples.Encryption.Worker;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = new EncryptionCodec() },
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

// Run workflow
var result = await client.ExecuteWorkflowAsync(
    (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
    new(id: "encryption-workflow-id", taskQueue: "encryption-sample"));
Console.WriteLine("Workflow result: {0}", result);