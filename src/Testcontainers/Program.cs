using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.Testcontainers;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Requires POSTGRES_CONNECTION_STRING environment variable
    var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING not set");
    var activities = new OrderActivities(connStr);

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue: "testcontainers-sample").
            AddActivity(activities.CheckInventoryAsync).
            AddActivity(activities.ReserveInventoryAsync).
            AddActivity(activities.ReleaseInventoryAsync).
            AddActivity(activities.ProcessPaymentAsync).
            AddActivity(activities.CreateOrderAsync).
            AddActivity(activities.SendConfirmationAsync).
            AddWorkflow<OrderWorkflow>());
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync()
{
    Console.WriteLine("Executing workflow");
    var result = await client.ExecuteWorkflowAsync(
        (OrderWorkflow wf) => wf.RunAsync(new OrderRequest("widget-1", 5, "credit-card")),
        new(id: $"order-{Guid.NewGuid()}", taskQueue: "testcontainers-sample"));
    Console.WriteLine("Order result: {0} - {1}", result.OrderId, result.Status);
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
