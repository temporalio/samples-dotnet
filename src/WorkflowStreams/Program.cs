using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.WorkflowStreams;

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

async Task RunWorkerAsync(bool llm)
{
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    var workerOptions = new TemporalWorkerOptions(
        llm ? WorkflowStreamsConstants.LlmTaskQueue : WorkflowStreamsConstants.TaskQueue);
    if (llm)
    {
        workerOptions.
            AddActivity(LlmActivities.StreamCompletionAsync).
            AddWorkflow<LlmWorkflow>();
    }
    else
    {
        workerOptions.
            AddActivity(PaymentActivities.ChargeCardAsync).
            AddWorkflow<HubWorkflow>().
            AddWorkflow<OrderWorkflow>().
            AddWorkflow<PipelineWorkflow>().
            AddWorkflow<TickerWorkflow>();
    }

    Console.WriteLine($"Running {(llm ? "LLM " : string.Empty)}worker");
    using var worker = new TemporalWorker(client, workerOptions);
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync(llm: false);
        break;
    case "publisher":
        await Scenarios.RunPublisherAsync(client);
        break;
    case "listener":
        await Scenarios.RunListenerAsync(client);
        break;
    case "reconnecting":
        await Scenarios.RunReconnectingSubscriberAsync(client);
        break;
    case "external-publisher":
        await Scenarios.RunExternalPublisherAsync(client);
        break;
    case "ticker":
        await Scenarios.RunTruncatingTickerAsync(client);
        break;
    case "llm-worker":
        await RunWorkerAsync(llm: true);
        break;
    case "llm":
        await Scenarios.RunLlmAsync(
            client,
            args.Length > 1 ? string.Join(' ', args.Skip(1)) : null);
        break;
    default:
        throw new ArgumentException(
            "Must pass 'worker', 'publisher', 'listener', 'reconnecting', " +
            "'external-publisher', 'ticker', 'llm-worker', or 'llm'");
}
