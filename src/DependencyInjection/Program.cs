using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.DependencyInjection;

async Task RunWorkerAsync()
{
    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(ctx =>
            ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureServices(ctx =>
            ctx.
                // Add the database client at the scoped level
                AddScoped<IMyDatabaseClient, MyDatabaseClient>().
                // Add the worker
                AddHostedTemporalWorker(
                    clientTargetHost: "localhost:7233",
                    clientNamespace: "default",
                    taskQueue: "dependency-injection-sample").
                // Add the activities class at the scoped level
                AddScopedActivities<MyActivities>().
                AddWorkflow<MyWorkflow>())
        .Build();
    await host.RunAsync();
}

async Task ExecuteWorkflowAsync()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    if (string.IsNullOrEmpty(connectOptions.TargetHost))
    {
        connectOptions.TargetHost = "localhost:7233";
    }
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

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "workflow":
        await ExecuteWorkflowAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}