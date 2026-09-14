using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkflowStreams.LlmTokenStreaming;

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

if (args.ElementAtOrDefault(0) == "worker")
{
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(Constants.TaskQueue).
            AddActivity(LlmActivities.StreamCompletionAsync).
            AddWorkflow<LlmWorkflow>());
    await worker.ExecuteAsync(tokenSource.Token);
}
else
{
    await Scenario.RunLlmAsync(
        client,
        args.Length > 0 ? string.Join(' ', args) : null);
}
