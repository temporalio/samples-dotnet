using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.Bedrock.Basic;

async Task RunWorkerAsync()
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.
        SetMinimumLevel(LogLevel.Information).
        AddSimpleConsole(options => options.SingleLine = true);

    builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ => new AmazonBedrockRuntimeClient());
    builder.Services.AddSingleton<BedrockActivities>();

    builder.Services.
        AddHostedTemporalWorker(clientTargetHost: "localhost:7233", clientNamespace: "default", taskQueue: "basic-bedrock-task-queue").
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
    var workflowId = "basic-bedrock-workflow";

    // Start the workflow
    var result = await client.ExecuteWorkflowAsync(
        (BedrockWorkflow workflow) =>
        workflow.RunAsync(new(prompt)),
        new WorkflowOptions(workflowId, "basic-bedrock-task-queue"));

    Console.WriteLine($"Result: {result.Response}");
}

async Task<ITemporalClient> CreateClientAsync() =>
    await TemporalClient.ConnectAsync(new("localhost:7233")
    {
        LoggerFactory = LoggerFactory.Create(builder =>
            builder.
                AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                SetMinimumLevel(LogLevel.Information)),
    });

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