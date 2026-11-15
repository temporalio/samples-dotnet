using System.CommandLine;
using Amazon.BedrockRuntime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.Bedrock.Basic;

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.
        SetMinimumLevel(LogLevel.Information).
        AddSimpleConsole(options => options.SingleLine = true);

    builder.Services.AddSingleton<IAmazonBedrockRuntime>(_ => new AmazonBedrockRuntimeClient());

    builder.Services.
        AddHostedTemporalWorker(clientTargetHost: "localhost:7233", clientNamespace: "default", taskQueue: "basic-bedrock-task-queue").
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
    var workflowId = "basic-bedrock-workflow";

    // Start the workflow
    var result = await client.ExecuteWorkflowAsync(
        (BedrockWorkflow workflow) =>
        workflow.RunAsync(new(prompt)),
        new WorkflowOptions(workflowId, "basic-bedrock-task-queue"));

    Console.WriteLine($"Result: {result.Response}");
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

var rootCommand = new RootCommand("Bedrock basic sample") { workerCommand, sendMessageCommand };
return await rootCommand.Parse(args).InvokeAsync();
