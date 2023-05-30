using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

Task RunWorkerAsync()
{
    IHost host = Host.CreateDefaultBuilder(args)
        // Add logging
        .ConfigureLogging(ctx => ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureServices(ctx =>
        {
            // Setup initial worker options
            ctx.Configure<TemporalWorkerOptions>(options =>
            {
                options.TaskQueue = MyWorkflow.TaskQueue;
                options.AddWorkflow(typeof(MyWorkflow));
            });

            // Add my database client
            ctx.AddScoped<IMyDatabaseClient>(_ => new IMyDatabaseClient.Core());

            // Add the activity whose class will only be created once
            ctx.AddTemporalActivitySingleton<MyActivitiesSingleton>();

            // Add the activity whose class will be created for each call
            ctx.AddTemporalActivityTransient<MyActivitiesTransient>();

            // Add the worker
            ctx.AddHostedService<Worker>();
        })
        .Build();

    return host.RunAsync();
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

/// <summary>
/// Worker host service.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILoggerFactory loggerFactory;
    private readonly TemporalWorkerOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Worker"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="options">Worker options.</param>
    /// <param name="activityOptions">Activity options.</param>
    public Worker(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IOptions<TemporalWorkerOptions> options,
        IOptions<ActivityOptions> activityOptions)
    {
        this.loggerFactory = loggerFactory;
        this.options = options.Value;
        activityOptions.Value.ApplyToWorkerOptions(serviceProvider, this.options);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = await TemporalClient.ConnectAsync(new()
        {
            TargetHost = "localhost:7233",
            LoggerFactory = loggerFactory,
        });
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(stoppingToken);
    }
}