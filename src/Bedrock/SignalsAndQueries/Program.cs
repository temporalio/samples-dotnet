using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.Bedrock.SignalsAndQueries;

async Task RunWorkerAsync()
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
    await app.RunAsync();
}

async Task SendMessageAsync()
{
    var prompt = args.ElementAtOrDefault(1);
    if (prompt is null)
    {
        Console.WriteLine("Usage: dotnet run send-message '<prompt>'");
        Console.WriteLine("Example: dotnet run send-message 'What animals are marsupials?'");
        return;
    }

    var client = await CreateClientAsync();
    var workflowId = "bedrock-workflow-with-signals";
    var inactivityTimeoutMinutes = 1;

    // Sends a signal to the workflow (and starts it if needed)
    var workflowOptions = new WorkflowOptions(workflowId, "with-signals-bedrock-task-queue");
    workflowOptions.SignalWithStart((BedrockWorkflow workflow) => workflow.UserPromptAsync(new(prompt)));
    await client.StartWorkflowAsync((BedrockWorkflow workflow) => workflow.RunAsync(new(inactivityTimeoutMinutes)), workflowOptions);
}

async Task GetHistoryAsync()
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

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "send-message":
        await SendMessageAsync();
        break;
    case "get-history":
        await GetHistoryAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'send-message' as the single argument");
}