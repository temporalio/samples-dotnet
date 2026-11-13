// @@@SNIPSTART dotnet-microsoft-agent-framework-mcp-worker
using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Mcp;

const string TaskQueue = "microsoft-agent-framework-mcp";

// The factory below runs inside the first model Activity, so check the key before the worker
// starts; otherwise that Activity would fail on every attempt while the starter waits.
if (args.ElementAtOrDefault(0) == "worker" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
{
    await Console.Error.WriteLineAsync("Set OPENAI_API_KEY before starting the worker.");
    return 1;
}

// `dotnet run mcp-server` serves the echo tool over stdio. The worker starts this same program as
// a subprocess, so this branch runs before anything else can write to stdout.
if (args.ElementAtOrDefault(0) == "mcp-server")
{
    await EchoMcpServer.RunAsync();
    return 0;
}

// Both factories run on the worker on first use. OPENAI_API_KEY and the server process never
// reach the Workflow, which carries only the model and server names.
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
    McpServers = new Dictionary<string, Func<IClientTransport>>
    {
        // The worker connects on the first MCP Activity, keeps the connection for 5 minutes of
        // idleness, and closes it when the worker stops. Swap in any MCP server here.
        ["echo"] = () => new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "echo",
            Command = "dotnet",
            Arguments = new List<string> { typeof(EchoMcpServer).Assembly.Location, "mcp-server" },
        }),
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
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<McpWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}
// @@@SNIPEND
async Task ExecuteWorkflowAsync()
{
    var result = await client.ExecuteWorkflowAsync(
        (McpWorkflow wf) => wf.RunAsync("Use the echo tool to echo the message 'hello from MCP'."),
        new(id: "microsoft-agent-framework-mcp", taskQueue: TaskQueue));
    Console.WriteLine($"Result: {result}");
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
        throw new ArgumentException("Must pass 'worker', 'workflow' or 'mcp-server' as the single argument");
}

return 0;
