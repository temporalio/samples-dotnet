using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.Bedrock.Basic;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

async Task RunWorkerAsync()
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.
        SetMinimumLevel(LogLevel.Information).
        AddSimpleConsole(options => options.SingleLine = true);

    builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ => new AmazonBedrockRuntimeClient());
    builder.Services.AddSingleton<IBasicBedrockActivities, BasicBedrockActivities>();

    builder.Services.
        AddHostedTemporalWorker(clientTargetHost: "localhost:7233", clientNamespace: "default", taskQueue: "bedrock-task-queue").
        AddSingletonActivities<IBasicBedrockActivities>().
        AddWorkflow<BasicBedrockWorkflow>();

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

    var workflowId = "basic-bedrock-workflow";

    await client.StartWorkflowAsync(
        (BasicBedrockWorkflow workflow) =>
        workflow.RunAsync(new BasicBedrockWorkflowArgs(prompt)),
        new WorkflowOptions(workflowId, "bedrock-task-queue"));
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "send-message":
        await SendMessageAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'send-message' as the single argument");
}