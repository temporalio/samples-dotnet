using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.StructuredOutput;

const string TaskQueue = "microsoft-agent-framework-structured-output";

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
                // The plugin fails a throwing factory with a non-retryable
                // MicrosoftAgentFrameworkConfigurationError, so the run fails in one attempt.
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
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<StructuredOutputWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
    var person = await client.ExecuteWorkflowAsync(
        (StructuredOutputWorkflow wf) => wf.RunAsync("John Smith is a 30 year-old software engineer."),
        new(id: "microsoft-agent-framework-structured-output", taskQueue: TaskQueue));
    Console.WriteLine($"Name={person.Name} Age={person.Age} Occupation={person.Occupation}");
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}

return 0;
