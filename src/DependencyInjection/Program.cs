using System.CommandLine;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using Temporalio.Runtime;
using TemporalioSamples.DependencyInjection;

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(ctx =>
            ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureServices(ctx =>
        {
            // Add the database client at the scoped level
            ctx.AddScoped<IMyDatabaseClient, MyDatabaseClient>();

            // Add the client the worker will use. The Core SDK writes its logs to the console
            // itself unless Forwarding is set, in which case they go to the injected ILogger.
            ctx.AddTemporalClient(
                clientTargetHost: "localhost:7233",
                clientNamespace: "default").
                Configure<ILoggerFactory>((options, loggerFactory) =>
                    options.Runtime = new TemporalRuntime(new TemporalRuntimeOptions()
                    {
                        Telemetry = new TelemetryOptions()
                        {
                            Logging = new LoggingOptions()
                            {
                                // Core SDK logs default to WARN; lowered here so there is more to see.
                                Filter = new TelemetryFilterOptions(core: TelemetryFilterOptions.Level.Info),
                                Forwarding = new LogForwardingOptions(loggerFactory.CreateLogger("Temporalio.Core")),
                            },
                        },
                    }));

            // Add the worker, which uses the injected client
            ctx.AddHostedTemporalWorker("dependency-injection-sample").
                // Add the activities class at the scoped level
                AddScopedActivities<MyActivities>().
                AddWorkflow<MyWorkflow>();
        })
        .Build();
    await host.RunAsync(cancellationToken);
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    var client = await TemporalClient.ConnectAsync(connectOptions);

    Console.WriteLine("Executing workflow");
    var result = await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "dependency-injection-workflow-id", taskQueue: "dependency-injection-sample"));

    Console.WriteLine("Workflow result: {0}", result);
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Dependency injection sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
