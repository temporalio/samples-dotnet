using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.Worker;
using TemporalioSamples.MicrosoftAgentFramework.HumanInTheLoop;

const string TaskQueue = "microsoft-agent-framework-human-in-the-loop";

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
                throw new InvalidOperationException("OPENAI_API_KEY must be set before starting the worker");
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
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<HumanInTheLoopWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

// @@@SNIPSTART dotnet-microsoft-agent-framework-human-in-the-loop-client
async Task ExecuteWorkflowAsync(bool approve)
{
    var handle = await client.StartWorkflowAsync(
        (HumanInTheLoopWorkflow wf) => wf.RunAsync("Please delete /tmp/report.txt"),
        new(id: "microsoft-agent-framework-human-in-the-loop", taskQueue: TaskQueue));

    // Poll the Query until the agent asks for approval.
    IReadOnlyList<ApprovalRequest> requests = Array.Empty<ApprovalRequest>();
    for (var attempt = 0; attempt < 120 && requests.Count == 0; attempt++)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        requests = await handle.QueryAsync(wf => wf.PendingApprovals());
    }

    if (requests.Count == 0)
    {
        throw new InvalidOperationException("The agent never asked for approval");
    }

    foreach (var request in requests)
    {
        var arguments = string.Join(", ", request.Arguments.Select(argument => $"{argument.Key}={argument.Value}"));
        Console.WriteLine($"Approval requested: {request.ToolName}({arguments})");
        var outcome = await handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision(request.RequestId, approve)));
        Console.WriteLine($"Decision: {outcome}");
    }

    Console.WriteLine($"Result: {await handle.GetResultAsync()}");
}
// @@@SNIPEND
switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        // `dotnet run workflow reject` shows the rejection path.
        await ExecuteWorkflowAsync(approve: args.ElementAtOrDefault(1) != "reject");
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the first argument");
}
