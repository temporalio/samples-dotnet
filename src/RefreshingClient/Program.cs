using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.RefreshingClient;

async Task<TemporalClient> CreateClientAsync()
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
    return await TemporalClient.ConnectAsync(connectOptions);
}

async Task RunWorkerAsync(TemporalClient client)
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Create an activity instance with some state
    var activities = new MyActivities();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "activity-simple-sample").
            AddActivity(activities.SelectFromDatabaseAsync).
            AddActivity(MyActivities.DoStaticThing).
            AddWorkflow<MyWorkflow>());

    var replaceWorkerClient = (TemporalClient newClient) =>
    {
        worker.Client = newClient;
        return Task.FromResult(true);
    };

    try
    {
        await Task.WhenAll(ClientRefreshAsync(replaceWorkerClient, tokenSource.Token), worker.ExecuteAsync(tokenSource.Token));
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(TemporalClient client)
{
    Console.WriteLine("Executing workflow");
    await client.ExecuteWorkflowAsync(
        (MyWorkflow wf) => wf.RunAsync(),
        new(id: "activity-simple-workflow-id", taskQueue: "activity-simple-sample"));
}

async Task ClientRefreshAsync(Func<TemporalClient, Task> asyncFunc, CancellationToken cancellationToken)
{
    Console.WriteLine("This program will refresh its Temporal client every 10 seconds.");
    await RunRecurringTaskAsync(TimeSpan.FromSeconds(10), cancellationToken, asyncFunc);
}

async Task RunRecurringTaskAsync(TimeSpan interval, CancellationToken cancellationToken, Func<TemporalClient, Task> asyncFunc)
{
    await Task.Delay(interval, cancellationToken);
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("Refreshing client...");
        try
        {
            var client = await CreateClientAsync();
            await asyncFunc(client);
            await Task.Delay(interval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Task cancelled.");
            break;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            // Continue running even if one iteration fails
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }
}

var client = await CreateClientAsync();
switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync(client);
        break;
    case "workflow":
        await ExecuteWorkflowAsync(client);
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'workflow' as the single argument");
}