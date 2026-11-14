using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Tools;

const string TaskQueue = "microsoft-agent-framework-tools";

var verb = args.ElementAtOrDefault(0);
if (verb is not ("worker" or "workflow"))
{
    await Console.Error.WriteLineAsync("Usage: dotnet run worker | dotnet run workflow");
    return 1;
}

// The factory below runs inside the first model Activity, so check the key before the worker
// starts; otherwise that Activity would fail on every attempt while the starter waits.
if (verb == "worker" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
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

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    Console.WriteLine("Running worker");
    // @@@SNIPSTART dotnet-microsoft-agent-framework-tools-worker
    // The Activity behind ActivityAsTool is registered like any other Activity.
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).
            AddActivity(WeatherActivities.GetWeatherAsync).
            AddWorkflow<ToolsWorkflow>());
    // @@@SNIPEND
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
    var result = await client.ExecuteWorkflowAsync(
        (ToolsWorkflow wf) => wf.RunAsync(
            "Please do two things:\n" +
            "1. Count the letter R's in 'strawberry'.\n" +
            "2. Look up the weather in San Francisco."),
        new(id: "microsoft-agent-framework-tools", taskQueue: TaskQueue));
    Console.WriteLine($"Result: {result}");
}

if (verb == "worker")
{
    await RunWorkerAsync();
}
else
{
    await ExecuteWorkflowAsync();
}

return 0;
