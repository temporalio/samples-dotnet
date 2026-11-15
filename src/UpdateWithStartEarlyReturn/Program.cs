using System.CommandLine;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.UpdateWithStartEarlyReturn;

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

const string TaskQueue = "update-with-start-early-return";

async Task RunWorkerAsync(ParseResult parseResult, CancellationToken cancellationToken)
{
    var client = await ConnectClientAsync();

    // Run worker until cancelled
    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(
        client,
        new TemporalWorkerOptions(TaskQueue).
            AddAllActivities(typeof(Activities), null).
            AddWorkflow<PaymentWorkflow>());
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

    const string paymentId = "my-early-return-payment-123";
    Console.WriteLine("Starting payment processing and waiting for authorize");

    // Issue payment for 500 as an update with start which will only wait until
    // the update (i.e. authorize) is complete, not the whole workflow like
    // traditional workflow runs
    var startOperation = WithStartWorkflowOperation.Create(
        (PaymentWorkflow wf) => wf.RunAsync(new(500)),
        new(id: paymentId, taskQueue: TaskQueue)
        {
            // Unlike the lazy-init sample, we want to fail if this is already
            // running, our only purpose here is early return, not get-or-create
            // logic
            IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.Fail,
        });
    await client.ExecuteUpdateWithStartWorkflowAsync(
        (PaymentWorkflow wf) => wf.WaitUntilAuthorizedAsync(),
        new(startOperation));
    Console.WriteLine("Payment authorized, can move on while rest of payment processing finishes...");

    // Go ahead and wait for payment to be complete (we don't have to do this,
    // we're only doing it for the sample)
    var handle = await startOperation.GetHandleAsync();
    await handle.GetResultAsync();
    Console.WriteLine("Payment processing complete");
}

// Cancellation token passed to actions is cancelled on ctrl+c
var workerCommand = new Command("worker", "Run worker");
workerCommand.SetAction(RunWorkerAsync);

var workflowCommand = new Command("workflow", "Execute workflow");
workflowCommand.SetAction(ExecuteWorkflowAsync);

var rootCommand = new RootCommand("Update with start early return sample") { workerCommand, workflowCommand };
return await rootCommand.Parse(args).InvokeAsync();
