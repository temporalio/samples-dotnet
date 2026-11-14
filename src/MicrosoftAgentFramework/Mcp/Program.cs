using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.Mcp;

const string TaskQueue = "microsoft-agent-framework-mcp";

var verb = args.ElementAtOrDefault(0);
if (verb is not ("worker" or "workflow" or "mcp-server"))
{
    await Console.Error.WriteLineAsync("Usage: dotnet run worker | dotnet run workflow | dotnet run mcp-server");
    return 1;
}

// `dotnet run mcp-server` serves the echo tool over stdio. The worker starts this same program as
// a subprocess, so this branch runs before anything else can write to stdout.
if (verb == "mcp-server")
{
    await EchoMcpServer.RunAsync();
    return 0;
}

// The factory below runs inside the first model Activity, so check the key before the worker
// starts; otherwise that Activity would fail on every attempt while the starter waits.
if (verb == "worker" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
{
    await Console.Error.WriteLineAsync("Set OPENAI_API_KEY before starting the worker.");
    return 1;
}

// @@@SNIPSTART dotnet-microsoft-agent-framework-mcp-worker
// MCP transports are registered on the worker by name; the Workflow carries only the name. The
// worker connects on the first MCP Activity, keeps the connection for 5 minutes of idleness, and
// closes it when the worker stops. Swap in any MCP server here. This one is the same program with
// the mcp-server verb: under `dotnet run`, a publish or a single-file publish the process is the
// app host, which takes the verb directly; under `dotnet <dll>` it is the dotnet muxer, which
// needs the DLL from AppContext.BaseDirectory.
var host = Environment.ProcessPath ?? "dotnet";
var hostIsMuxer = string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase);
var mcpServers = new Dictionary<string, Func<IClientTransport>>
{
    ["echo"] = () => new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "echo",
        Command = host,
        Arguments = hostIsMuxer
            ? new List<string> { Path.Combine(AppContext.BaseDirectory, "TemporalioSamples.MicrosoftAgentFramework.Mcp.dll"), "mcp-server" }
            : new List<string> { "mcp-server" },
    }),
};
// @@@SNIPEND
// The chat client factory runs on the worker on first use. OPENAI_API_KEY never reaches the
// Workflow, which carries only the model name.
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
    McpServers = mcpServers,
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

async Task ExecuteWorkflowAsync()
{
    var result = await client.ExecuteWorkflowAsync(
        (McpWorkflow wf) => wf.RunAsync("Use the echo tool to echo the message 'hello from MCP'."),
        new(id: "microsoft-agent-framework-mcp", taskQueue: TaskQueue));
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
