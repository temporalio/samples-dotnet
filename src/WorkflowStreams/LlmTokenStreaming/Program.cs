using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

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
        new TemporalWorkerOptions(Constants.TaskQueue).
            AddActivity(LlmActivities.StreamCompletionAsync).
            AddWorkflow<LlmWorkflow>());
    await worker.ExecuteAsync(cancellationToken);
}

var promptArgument = new Argument<string[]>("prompt")
{
    Description = "Prompt to send to the LLM (words are joined with spaces)",
    Arity = ArgumentArity.ZeroOrMore,
};

async Task RunScenarioAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    var prompt = parseResult.GetValue(promptArgument);
    await Scenario.RunLlmAsync(
        client,
        prompt is { Length: > 0 } ? string.Join(' ', prompt) : null);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var rootCommand = new RootCommand("Workflow streams LLM token streaming sample") { workerCommand, promptArgument };
rootCommand.SetAction(RunScenarioAsync);
return await rootCommand.Parse(args).InvokeAsync();
