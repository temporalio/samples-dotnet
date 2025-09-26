using System.Globalization;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Client.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.UpdateWithStartLazyInit;

// Create a client to localhost on default namespace
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

const string TaskQueue = "update-with-start-lazy-init";

async Task RunWorkerAsync()
{
    // Cancellation token cancelled on ctrl+c
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).
            AddAllActivities(typeof(Activities), null).
            AddWorkflow<ShoppingCartWorkflow>());
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
    Console.WriteLine("Starting to shop...");

    // Add 3 of an item
    var addResult = await AddCartItemAsync("session-777", new(Sku: "sku-123", 3));
    Console.WriteLine($"Subtotal after item 1: {addResult.SubtotalString}");

    // Add 2 of another (that is not found)
    addResult = await AddCartItemAsync("session-777", new(Sku: "sku-456", 2));
    Console.WriteLine($"Subtotal after item 2: {addResult.SubtotalString}");

    // Checkout and display final order
    await addResult.WorkflowHandle.SignalAsync(wf => wf.CheckoutAsync());
    var finalOrder = await addResult.WorkflowHandle.GetResultAsync();
    Console.WriteLine($"Final order: {finalOrder}");
}

async Task<AddCartItemResult> AddCartItemAsync(string sessionId, ShoppingCartItem item)
{
    // Issue an update-with-start that will create the workflow if it does not
    // exist before attempting the update

    // Create the start operation
    var startOperation = WithStartWorkflowOperation.Create(
        (ShoppingCartWorkflow wf) => wf.RunAsync(),
        new(id: $"cart-{sessionId}", taskQueue: TaskQueue)
        {
            IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
        });

    // Issue the update-with-start, swallowing item-unavailable failure
    decimal? subtotal;
    try
    {
        subtotal = await client.ExecuteUpdateWithStartWorkflowAsync(
            (ShoppingCartWorkflow wf) => wf.AddItemAsync(item),
            new(startOperation));
    }
    catch (WorkflowUpdateFailedException e) when (
        e.InnerException is ApplicationFailureException appErr && appErr.ErrorType == "ItemUnavailable")
    {
        // Set subtotal to null if item was not found
        subtotal = null;
    }

    return new(await startOperation.GetHandleAsync(), subtotal);
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

public record AddCartItemResult(
    WorkflowHandle<ShoppingCartWorkflow, ShoppingCartWorkflow.FinalizedOrder> WorkflowHandle,
    decimal? Subtotal)
{
    public string SubtotalString => Subtotal?.ToString(CultureInfo.CurrentCulture) ?? "<item not found>";
}