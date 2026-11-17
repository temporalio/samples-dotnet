using System.ClientModel;
using System.ClientModel.Primitives;
using System.CommandLine;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.Extensions.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.ContinueAsNew;

const string TaskQueue = "microsoft-agent-framework-chat";
const string WorkflowId = "microsoft-agent-framework-chat";

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
    // The factory below runs inside the first model Activity, so check the key before the worker
    // starts; otherwise that Activity would fail on every attempt while the starter waits.
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
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

    var client = await ConnectClientAsync(plugin);
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<ChatWorkflow>());
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

var promptArgument = new Argument<string>("prompt") { Description = "The next turn of the chat" };

// @@@SNIPSTART dotnet-microsoft-agent-framework-continue-as-new-client
async Task<int> SendMessageAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var prompt = parseResult.GetRequiredValue(promptArgument);
    if (string.IsNullOrWhiteSpace(prompt))
    {
        await Console.Error.WriteLineAsync("The prompt is empty.");
        return 1;
    }

    // Update-With-Start: one request starts the chat if it is not running and sends the turn.
    // The reply comes back from the same call.
    var client = await ConnectClientAsync();
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
    return 0;
}

async Task<int> EndChatAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();
    var handle = client.GetWorkflowHandle<ChatWorkflow>(WorkflowId);
    try
    {
        var transcript = await handle.QueryAsync(wf => wf.Transcript());
        Console.WriteLine($"{transcript.Count} messages in the session:");
        foreach (var line in transcript)
        {
            Console.WriteLine(line);
        }

        await handle.SignalAsync(wf => wf.EndChatAsync());
    }
    catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
    {
        await Console.Error.WriteLineAsync("No chat is running. Start one with: dotnet run send-message '<prompt>'");
        return 1;
    }

    await handle.GetResultAsync();
    Console.WriteLine("Chat ended");
    return 0;
}
// @@@SNIPEND
var workerCommand = new Command("worker", "Run the worker");
workerCommand.SetAction(RunWorkerAsync);

var sendMessageCommand = new Command("send-message", "Send one turn of the chat and print the reply") { promptArgument };
sendMessageCommand.SetAction(SendMessageAsync);

var endChatCommand = new Command("end-chat", "End the chat and print its transcript");
endChatCommand.SetAction(EndChatAsync);

var rootCommand = new RootCommand("Microsoft Agent Framework continue-as-new sample") { workerCommand, sendMessageCommand, endChatCommand };
return await rootCommand.Parse(args).InvokeAsync();
