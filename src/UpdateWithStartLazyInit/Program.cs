using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Exceptions;
using Temporalio.Worker;
using TemporalioSamples.UpdateWithStartLazyInit;

async Task<TemporalClient> ConnectClientAsync()
{
    // Create a client to localhost on default namespace
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    connectOptions.TargetHost ??= "localhost:7233";
    connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information));
    return await TemporalClient.ConnectAsync(connectOptions);
}

const string TaskQueue = "update-with-start-lazy-init";

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).
            AddAllActivities(typeof(Activities), null).
            AddWorkflow<ShoppingCartWorkflow>());
    try
    {
        await worker.ExecuteAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task ExecuteWorkflowAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    Console.WriteLine("Starting to shop...");

    // Add 3 of an item
    var addResult = await AddCartItemAsync(client, "session-777", new(Sku: "sku-123", 3));
    Console.WriteLine($"Subtotal after item 1: {addResult.SubtotalString}");

    // Add 2 of another (that is not found)
    addResult = await AddCartItemAsync(client, "session-777", new(Sku: "sku-456", 2));
    Console.WriteLine($"Subtotal after item 2: {addResult.SubtotalString}");

    // Checkout and display final order
    await addResult.WorkflowHandle.SignalAsync(wf => wf.CheckoutAsync());
    var finalOrder = await addResult.WorkflowHandle.GetResultAsync();
    Console.WriteLine($"Final order: {finalOrder}");
}

async Task<AddCartItemResult> AddCartItemAsync(TemporalClient client, string sessionId, ShoppingCartItem item)
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

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Update with start lazy init sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();

public record AddCartItemResult(
    WorkflowHandle<ShoppingCartWorkflow, ShoppingCartWorkflow.FinalizedOrder> WorkflowHandle,
    decimal? Subtotal)
{
    public string SubtotalString => Subtotal?.ToString(CultureInfo.CurrentCulture) ?? "<item not found>";
}