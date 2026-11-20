using System.ClientModel;
using System.ClientModel.Primitives;
using System.CommandLine;
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

// The echo server is this same program with the mcp-server command. Under `dotnet run`, a publish or
// a single-file publish the process is the app host, which takes the command directly; under
// `dotnet <dll>` it is the dotnet muxer, which needs the DLL from AppContext.BaseDirectory.
(string Command, List<string> Arguments) EchoServerCommand()
{
    var host = Environment.ProcessPath ?? "dotnet";
    return string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase)
        ? (host, new List<string> { Path.Combine(AppContext.BaseDirectory, "TemporalioSamples.MicrosoftAgentFramework.Mcp.dll"), "mcp-server" })
        : (host, new List<string> { "mcp-server" });
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

    // @@@SNIPSTART dotnet-microsoft-agent-framework-mcp-worker
    // MCP transports are registered on the worker by name; the Workflow carries only the name. The
    // worker connects on the first MCP Activity, keeps the connection for 5 minutes of idleness, and
    // closes it when the worker stops. Swap in any MCP server here; this one is the sample's own echo
    // server over stdio.
    var (command, arguments) = EchoServerCommand();
    var mcpServers = new Dictionary<string, Func<IClientTransport>>
    {
        ["echo"] = () => new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "echo",
            Command = command,
            Arguments = arguments,
        }),
    };
    // @@@SNIPEND

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
        McpServers = mcpServers,
    });

    var client = await ConnectClientAsync(plugin);
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<McpWorkflow>());
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
        (McpWorkflow wf) => wf.RunAsync("Use the echo tool to echo the message 'hello from MCP'."),
        new(id: "microsoft-agent-framework-mcp", taskQueue: TaskQueue));
    Console.WriteLine($"Result: {result}");
}

async Task ServeMcpAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    // The worker starts this same program as a subprocess and talks to it over stdio, so this
    // command must not write anything else to stdout.
    await EchoMcpServer.RunAsync(cancellationToken);
}

var workerCommand = new Command("worker", "Run the worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Run the Workflow and print its result");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var mcpServerCommand = new Command("mcp-server", "Serve the echo tool over stdio");
mcpServerCommand.SetAction(ServeMcpAsync);

var rootCommand = new RootCommand("Microsoft Agent Framework MCP sample") { workerCommand, workflowCommand, mcpServerCommand };
return await rootCommand.Parse(args).InvokeAsync();
