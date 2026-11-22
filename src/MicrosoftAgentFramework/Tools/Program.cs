using System.ClientModel;
using System.ClientModel.Primitives;
using System.CommandLine;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Tools;

const string TaskQueue = "microsoft-agent-framework-tools";

async Task<TemporalClient> ConnectClientAsync(MicrosoftAgentFrameworkPlugin? plugin = null)
{
    // Create a client to localhost on default namespace. The worker passes the plugin, which puts
    // the model Activities on every worker built from the client; the starter needs none.
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    if (plugin is not null)
    {
        connectOptions.Plugins = new ITemporalClientPlugin[] { plugin };
    }

    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task<int> RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // Read the key once, before the worker starts: a missing key would otherwise fail the first
    // model Activity on every attempt while the starter waits. The factory below captures it.
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
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
                // Temporal retries the model Activity, so the OpenAI client's own retries are off.
                var options = new OpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 0) };
                return new OpenAIClient(new ApiKeyCredential(apiKey), options).
                    GetChatClient("gpt-4o-mini").
                    AsIChatClient();
            },
        },
    });

    var client = await ConnectClientAsync(plugin);
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
        // The token is cancelled on ctrl+c.
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }

    return 0;
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();
    var result = await client.ExecuteWorkflowAsync(
        (ToolsWorkflow wf) => wf.RunAsync(
            "Please do two things:\n" +
            "1. Count the letter R's in 'strawberry'.\n" +
            "2. Look up the weather in San Francisco."),
        new(id: "microsoft-agent-framework-tools", taskQueue: TaskQueue));
    Console.WriteLine($"Result: {result}");
}

var workerCommand = new Command("worker", "Run the worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Run the Workflow and print its result");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Microsoft Agent Framework tools sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
