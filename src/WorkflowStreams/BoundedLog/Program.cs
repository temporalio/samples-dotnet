using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkflowStreams.BoundedLog;

async Task<TemporalClient> ConnectClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(Constants.TaskQueue).AddWorkflow<TickerWorkflow>());
    await worker.ExecuteAsync(cancellationToken);
}

async Task RunScenarioAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    await Scenario.RunTruncatingTickerAsync(client);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var rootCommand = new RootCommand("Workflow streams bounded log sample") { workerCommand };
rootCommand.SetAction(RunScenarioAsync);
return await rootCommand.Parse(args).InvokeAsync();
