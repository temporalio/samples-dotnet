using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Streaming;

const string TaskQueue = "microsoft-agent-framework-streaming";

// The factory below runs inside the first model Activity, so check the key before the worker
// starts; otherwise that Activity would fail on every attempt while the starter waits.
if (args.ElementAtOrDefault(0) == "worker" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
{
    await Console.Error.WriteLineAsync("Set OPENAI_API_KEY before starting the worker.");
    return 1;
}

// The factory runs on the worker on first use. OPENAI_API_KEY never reaches the Workflow, which
// carries only the model name.
var plugin = new MicrosoftAgentFrameworkPlugin(new MicrosoftAgentFrameworkPluginOptions
{
    ChatClients = new Dictionary<string, Func<IChatClient>>
    {
        ["gpt-4o-mini"] = () =>
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // The worker checked the key before it started, so this is a last resort: the
                // plugin logs a throwing factory and retries the model Activity under its retry policy.
                throw new InvalidOperationException("OPENAI_API_KEY is not set");
            }

            // Temporal retries the model Activity, so the OpenAI client's own retries are off.
            var options = new OpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 0) };
            return new OpenAIClient(new ApiKeyCredential(apiKey), options).
                GetChatClient("gpt-4o-mini").
                AsIChatClient();
        },
    },
});

// Create a client to localhost on default namespace. The plugin goes on the client; the worker
// built from it inherits the plugin's Activities.
var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
connectOptions.Plugins = new ITemporalClientPlugin[] { plugin };
var client = await TemporalClient.ConnectAsync(connectOptions);

if (args.ElementAtOrDefault(0) == "worker")
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<StreamingWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}
else
{
    // `dotnet run` and `dotnet run workflow` use the default prompt; any other arguments are the prompt.
    var prompt = args.Length == 0 || (args.Length == 1 && args[0] == "workflow")
        ? "Count from 1 to 5, one number per sentence."
        : string.Join(' ', args);
    await SubscribeAsync(prompt);
}

return 0;

// @@@SNIPSTART dotnet-microsoft-agent-framework-streaming-client
async Task SubscribeAsync(string prompt)
{
    const string workflowId = "microsoft-agent-framework-streaming";
    var handle = await client.StartWorkflowAsync(
        (StreamingWorkflow wf) => wf.RunAsync(new StreamingInput(prompt, null)),
        new(id: workflowId, taskQueue: TaskQueue));

    // Each item is one ChatResponseUpdate, published from the model Activity as the provider
    // produced it. The subscription ends when the Workflow closes.
    await using var streams = new WorkflowStreamClient(client, workflowId);
    var subscription = streams.SubscribeAsync<ChatResponseUpdate>(
        new WorkflowStreamSubscribeOptions { Topics = new List<string> { StreamingWorkflow.Topic } });
    string? responseId = null;
    await foreach (var item in subscription)
    {
        // A retried model Activity publishes its attempt from the start under a new ResponseId.
        if (responseId is not null && item.Value.ResponseId is { } id && id != responseId)
        {
            Console.WriteLine();
            Console.WriteLine("(the model Activity retried; starting over)");
        }

        responseId = item.Value.ResponseId ?? responseId;
        Console.Write(item.Value.Text);
    }

    Console.WriteLine();
    Console.WriteLine($"Final result: {await handle.GetResultAsync()}");
}
// @@@SNIPEND
