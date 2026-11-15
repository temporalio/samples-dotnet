using System.CommandLine;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.Bedrock.SignalsAndQueries;

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.
        SetMinimumLevel(LogLevel.Information).
        AddSimpleConsole(options => options.SingleLine = true);

    builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ => new AmazonBedrockRuntimeClient());

    builder.Services.
        AddHostedTemporalWorker(clientTargetHost: "localhost:7233", clientNamespace: "default", taskQueue: "with-signals-bedrock-task-queue").
        AddSingletonActivities<BedrockActivities>().
        AddWorkflow<BedrockWorkflow>();

    var app = builder.Build();
    await app.RunAsync(cancellationToken);
}

var promptArgument = new Argument<string>("prompt") { Description = "Prompt to send to the model" };

async Task SendMessageAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var prompt = parseResult.GetRequiredValue(promptArgument);

    var client = await CreateClientAsync();
    var workflowId = "bedrock-workflow-with-signals";
    var inactivityTimeoutMinutes = 1;

    // Sends a signal to the workflow (and starts it if needed)
    var workflowOptions = new WorkflowOptions(workflowId, "with-signals-bedrock-task-queue");
    workflowOptions.SignalWithStart((BedrockWorkflow workflow) => workflow.UserPromptAsync(new(prompt)));
    await client.StartWorkflowAsync((BedrockWorkflow workflow) => workflow.RunAsync(new(inactivityTimeoutMinutes)), workflowOptions);
}

async Task GetHistoryAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await CreateClientAsync();
    var workflowId = "bedrock-workflow-with-signals";
    var handle = client.GetWorkflowHandle<BedrockWorkflow>(workflowId);

    // Queries the workflow for the conversation history
    var history = await handle.QueryAsync(workflow => workflow.ConversationHistory);

    Console.WriteLine("Conversation History:");
    foreach (var entry in history)
    {
        Console.WriteLine($"{entry.Speaker}: {entry.Message}");
    }

    // Queries the workflow for the conversation summary
    var summary = await handle.QueryAsync(workflow => workflow.ConversationSummary);
    if (summary is not null)
    {
        Console.WriteLine("Conversation Summary:");
        Console.WriteLine(summary);
    }
}

async Task<ITemporalClient> CreateClientAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var sendMessageCommand = new Command("send-message", "Send a message") { promptArgument };
sendMessageCommand.SetAction(SendMessageAsync);

var getHistoryCommand = new Command("get-history", "Get conversation history");
getHistoryCommand.SetAction(GetHistoryAsync);

var rootCommand = new RootCommand("Bedrock signals and queries sample") { workerCommand, sendMessageCommand, getHistoryCommand };
return await rootCommand.Parse(args).InvokeAsync();
