using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.AspNet.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(ctx => ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
    .ConfigureServices(ctx => ctx.AddHostedService<Worker>())
    .Build();

host.Run();

public sealed class Worker : BackgroundService
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<Worker> logger;

    public Worker(ILoggerFactory loggerFactory, ILogger<Worker> logger)
    {
        this.loggerFactory = loggerFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var worker = new TemporalWorker(
            await TemporalClient.ConnectAsync(new()
            {
                TargetHost = "localhost:7233",
                LoggerFactory = loggerFactory,
            }),
            new TemporalWorkerOptions(taskQueue: MyWorkflow.TaskQueue).
                AddWorkflow<MyWorkflow>());
        await worker.ExecuteAsync(stoppingToken);
    }
}