using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;

const string TaskQueue = "microsoft-agent-framework-chat";
const string WorkflowId = "microsoft-agent-framework-chat";

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

async Task RunWorkerAsync()
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
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<ChatWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

// @@@SNIPSTART dotnet-microsoft-agent-framework-continue-as-new-client
async Task SendMessageAsync(string? prompt)
{
    if (prompt is null)
    {
        Console.WriteLine("Usage: dotnet run send-message '<prompt>'");
        return;
    }

    // Update-With-Start: one request starts the chat if it is not running and sends the turn.
    // The reply comes back from the same call.
    var startOperation = WithStartWorkflowOperation.Create(
        (ChatWorkflow wf) => wf.RunAsync(new ChatInput(null)),
        new(id: WorkflowId, taskQueue: TaskQueue)
        {
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
        });
    var reply = await client.ExecuteUpdateWithStartWorkflowAsync(
        (ChatWorkflow wf) => wf.TurnAsync(prompt),
        new(startOperation));
    Console.WriteLine($"user: {prompt}");
    Console.WriteLine($"assistant: {reply}");
}

async Task EndChatAsync()
{
    var handle = client.GetWorkflowHandle<ChatWorkflow>(WorkflowId);
    var transcript = await handle.QueryAsync(wf => wf.Transcript());
    Console.WriteLine($"{transcript.Count} messages in the session:");
    foreach (var line in transcript)
    {
        Console.WriteLine(line);
    }

    await handle.SignalAsync(wf => wf.EndChatAsync());
    await handle.GetResultAsync();
    Console.WriteLine("Chat ended");
}
// @@@SNIPEND
switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "send-message":
        await SendMessageAsync(args.ElementAtOrDefault(1));
        break;
    case "end-chat":
        await EndChatAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker', 'send-message' or 'end-chat' as the first argument");
}

return 0;
