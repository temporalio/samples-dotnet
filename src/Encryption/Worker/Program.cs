using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.Encryption.Codec;
using TemporalioSamples.Encryption.Worker;

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // Create a client to localhost on default namespace
    var client = await TemporalClient.ConnectAsync(new("localhost:7233")
    {
        DataConverter = DataConverter.Default with { PayloadCodec = new EncryptionCodec() },
        LoggerFactory = LoggerFactory.Create(builder =>
            builder.
                AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                SetMinimumLevel(LogLevel.Information)),
    });

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "encryption-sample").
            AddWorkflow<GreetingWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

// Cancellation token passed to the action is cancelled on ctrl+c
var rootCommand = new RootCommand("Encryption worker");
rootCommand.SetAction(RunWorkerAsync);
return await rootCommand.Parse(args).InvokeAsync();
