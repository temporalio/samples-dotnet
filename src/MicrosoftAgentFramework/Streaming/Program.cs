using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.MicrosoftAgentFramework;
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
                // Non-retryable: retrying the model Activity cannot supply the key.
                throw new ApplicationFailureException(
                    "OPENAI_API_KEY is not set",
                    errorType: MicrosoftAgentFrameworkErrorTypes.ConfigurationError,
                    nonRetryable: true);
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
    // Any other arguments are the prompt.
    await SubscribeAsync(args.Length > 0 ? string.Join(' ', args) : "Count from 1 to 5, one number per sentence.");
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
    await foreach (var item in subscription)
    {
        Console.Write(item.Value.Text);
    }

    Console.WriteLine();
    Console.WriteLine($"Final result: {await handle.GetResultAsync()}");
}
// @@@SNIPEND
