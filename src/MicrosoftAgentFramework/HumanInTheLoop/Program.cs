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
using TemporalioSamples.MicrosoftAgentFramework.HumanInTheLoop;

const string TaskQueue = "microsoft-agent-framework-human-in-the-loop";

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
        new TemporalWorkerOptions(TaskQueue).AddWorkflow<HumanInTheLoopWorkflow>());
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

// @@@SNIPSTART dotnet-microsoft-agent-framework-human-in-the-loop-client
var rejectOption = new Option<bool>("--reject")
{
    Description = "Reject the approval request instead of approving it",
};

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var approve = !parseResult.GetValue(rejectOption);
    var client = await ConnectClientAsync();
    var handle = await client.StartWorkflowAsync(
        (HumanInTheLoopWorkflow wf) => wf.RunAsync("Please delete /tmp/report.txt"),
        new(id: "microsoft-agent-framework-human-in-the-loop", taskQueue: TaskQueue));

    // Poll the Query while the Workflow runs and answer each request once; the agent may ask in
    // more than one round. Stop as soon as the Workflow closes: the agent may answer without
    // calling the tool, and a failed run throws from the result below.
    var result = handle.GetResultAsync();
    var decided = new HashSet<string>();
    while (!result.IsCompleted)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        if (result.IsCompleted)
        {
            break;
        }

        foreach (var request in await handle.QueryAsync(wf => wf.PendingApprovals()))
        {
            if (!decided.Add(request.RequestId))
            {
                continue;
            }

            var arguments = string.Join(", ", request.Arguments.Select(argument => $"{argument.Key}={argument.Value}"));
            Console.WriteLine($"Approval requested: {request.ToolName}({arguments})");
            var outcome = await handle.ExecuteUpdateAsync(wf => wf.DecideAsync(new Decision(request.RequestId, approve)));
            Console.WriteLine($"Decision: {outcome}");
        }
    }

    Console.WriteLine($"Result: {await result}");
}
// @@@SNIPEND
var workerCommand = new Command("worker", "Run the worker");
workerCommand.SetAction(RunWorkerAsync);

// `dotnet run workflow --reject` shows the rejection path.
var workflowCommand = new Command("workflow", "Run the Workflow, answer its approval request and print its result") { rejectOption };
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Microsoft Agent Framework human in the loop sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
