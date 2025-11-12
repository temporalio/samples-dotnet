using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Converters;
using TemporalioSamples.Encryption.Codec;
using TemporalioSamples.Encryption.Worker;

// Create a client to localhost on default namespace
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
if (string.IsNullOrEmpty(connectOptions.TargetHost))
{
    connectOptions.TargetHost = "localhost:7233";
}
connectOptions.DataConverter = DataConverter.Default with { PayloadCodec = new EncryptionCodec() };
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

// Run workflow
var result = await client.ExecuteWorkflowAsync(
    (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
    new(id: "encryption-workflow-id", taskQueue: "encryption-sample"));
Console.WriteLine("Workflow result: {0}", result);