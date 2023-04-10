using Temporalio.Client;
using Temporalio.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(ctx => ctx.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
    .ConfigureServices(ctx => ctx.AddHostedService<Worker>())
    .Build();

host.Run();

public sealed class Worker : BackgroundService
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<Worker> logger;
    private TemporalWorker? worker;

    public Worker(ILoggerFactory loggerFactory, ILogger<Worker> logger)
    {
        this.loggerFactory = loggerFactory;
        this.logger = logger;
    }

    public override void Dispose()
    {
        base.Dispose();
        worker?.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (worker != null)
        {
            throw new InvalidOperationException("Worker already started");
        }
        logger.LogInformation("Running Temporal worker");
        worker = new(
            await TemporalClient.ConnectAsync(new()
            {
                TargetHost = "localhost:7233",
                Namespace = "default",
                LoggerFactory = loggerFactory,
            }),
            new()
            {
                TaskQueue = MyWorkflow.TaskQueue,
                Workflows = { typeof(MyWorkflow) },
            });
        await worker.ExecuteAsync(stoppingToken);
    }
}