using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.ActivityDependencyInjection;

async Task ExecuteWorkflowAsync()
{
    Console.WriteLine("Executing workflow");
    var client = await TemporalClient.ConnectAsync(new("localhost:7233")
    {
        LoggerFactory = LoggerFactory.Create(builder =>
            builder.
                AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
                SetMinimumLevel(LogLevel.Information)),
    });
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "activity-di-workflow-id", taskQueue: MyWorkflow.TaskQueue));
}

async Task RunWorkerAsync()
{
    IHost host = Host.CreateDefaultBuilder(args).
        // Add logging
        ConfigureLogging(ctx => ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information)).
        ConfigureServices(ctx =>
        {
            // Set client options
            ctx.Configure<TemporalClientConnectOptions>(options => options.TargetHost = "localhost:7233");

            // Setup initial worker options
            ctx.Configure<TemporalWorkerOptions>(options =>
            {
                options.TaskQueue = MyWorkflow.TaskQueue;
                options.AddWorkflow<MyWorkflow>();
            });

            // Add my database client
            ctx.AddScoped<IMyDatabaseClient>(_ => new IMyDatabaseClient.Core());

            // Add the activity whose class will only be created once
            ctx.AddTemporalActivitySingleton<MyActivitiesSingleton>();

            // Add the activity whose class will be created for each call
            ctx.AddTemporalActivityTransient<MyActivitiesTransient>();

            // Add the worker
            ctx.AddHostedService<Worker>();
        }).
        Build();

    await host.RunAsync();
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