namespace TemporalioSamples.ActivityDependencyInjection;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Worker;

/// <summary>
/// Worker host service.
/// </summary>
public class Worker : BackgroundService
{
    private readonly TemporalClientConnectOptions connectOptions;
    private readonly TemporalWorkerOptions workerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="Worker"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    /// <param name="connectOptions">Connect options.</param>
    /// <param name="workerOptions">Worker options.</param>
    /// <param name="activities">Activity options.</param>
    public Worker(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IOptions<TemporalClientConnectOptions> connectOptions,
        IOptions<TemporalWorkerOptions> workerOptions,
        IOptions<ActivityCollection> activities)
    {
        this.connectOptions = connectOptions.Value;
        this.connectOptions.LoggerFactory = loggerFactory;
        this.workerOptions = workerOptions.Value;
        activities.Value.ApplyToWorkerOptions(serviceProvider, this.workerOptions);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = await TemporalClient.ConnectAsync(connectOptions);
        using var worker = new TemporalWorker(client, workerOptions);
        await worker.ExecuteAsync(stoppingToken);
    }
}