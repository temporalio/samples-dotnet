using System.ClientModel;
using System.ClientModel.Primitives;
using System.CommandLine;
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
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<StreamingWorkflow>());
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

var promptArgument = new Argument<string[]>("prompt")
{
    Description = "Prompt to send to the model (words are joined with spaces); the default counts to five",
    Arity = ArgumentArity.ZeroOrMore,
};

// @@@SNIPSTART dotnet-microsoft-agent-framework-streaming-client
async Task SubscribeAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var words = parseResult.GetValue(promptArgument);
    var prompt = words is { Length: > 0 } ? string.Join(' ', words) : "Count from 1 to 5, one number per sentence.";
    var client = await ConnectClientAsync();
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
    await foreach (var item in subscription.WithCancellation(cancellationToken))
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
var workerCommand = new Command("worker", "Run the worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Run the Workflow and print the streamed updates") { promptArgument };
workflowCommand.SetAction(SubscribeAsync);

var rootCommand = new RootCommand("Microsoft Agent Framework streaming sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
