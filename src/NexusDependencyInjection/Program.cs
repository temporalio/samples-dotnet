using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Hosting;
using TemporalioSamples.NexusDependencyInjection.Caller;
using TemporalioSamples.NexusDependencyInjection.Handler;

const string handlerTaskQueue = "nexus-handler-queue";
const string callerTaskQueue = "nexus-caller-queue";

// The namespace (and address, TLS, API key, etc.) come from environment configuration, e.g. the
// TEMPORAL_NAMESPACE environment variable or a temporal.toml profile. See the README.
TemporalClientConnectOptions LoadConnectOptions()
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    return connectOptions;
}

// The handler worker runs in the handler namespace and hosts the Nexus service. Because it runs as a
// generic host, the service handler's dependencies are injected by the container.
async Task RunHandlerWorkerAsync()
{
    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(ctx =>
            ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureServices(ctx =>
            ctx.
                // Add the dependency that will be injected into the Nexus service handler
                AddScoped<IGreetingClient, GreetingClient>().
                // Add the worker, connecting with options loaded from environment configuration
                AddHostedTemporalWorker(handlerTaskQueue).
                ConfigureOptions(options => options.ClientOptions = LoadConnectOptions()).
                // Add the Nexus service handler at the scoped level. Use AddSingletonNexusService or
                // AddTransientNexusService for singleton/transient lifetimes instead.
                AddScopedNexusService<GreetingServiceHandler>())
        .Build();
    await host.RunAsync();
}

// The caller worker runs in the caller namespace and hosts the workflow that invokes the Nexus
// operation.
async Task RunCallerWorkerAsync()
{
    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(ctx =>
            ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureServices(ctx =>
            ctx.
                AddHostedTemporalWorker(callerTaskQueue).
                ConfigureOptions(options => options.ClientOptions = LoadConnectOptions()).
                AddWorkflow<GreetingCallerWorkflow>())
        .Build();
    await host.RunAsync();
}

async Task ExecuteCallerWorkflowAsync()
{
    var connectOptions = LoadConnectOptions();
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    var client = await TemporalClient.ConnectAsync(connectOptions);

    Console.WriteLine("Executing workflow");
    var result = await client.ExecuteWorkflowAsync(
        (GreetingCallerWorkflow wf) => wf.RunAsync("Temporal"),
        new(id: "nexus-dependency-injection-workflow-id", taskQueue: callerTaskQueue));

    Console.WriteLine("Workflow result: {0}", result);
}

switch (args.ElementAtOrDefault(0))
{
    case "handler-worker":
        await RunHandlerWorkerAsync();
        break;
    case "caller-worker":
        await RunCallerWorkerAsync();
        break;
    case "caller-workflow":
        await ExecuteCallerWorkflowAsync();
        break;
    default:
        throw new ArgumentException(
            "Must pass 'handler-worker', 'caller-worker', or 'caller-workflow' as the single argument");
}
