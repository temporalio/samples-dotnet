using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Worker;
using TemporalioSamples.WorkerVersioning;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ")
            .SetMinimumLevel(LogLevel.Information)),
});

var taskQueue = $"worker-versioning-{Guid.NewGuid()}";

// Start a 1.0 worker
using var workerV1 = new TemporalWorker(
    client,
    new TemporalWorkerOptions(taskQueue) { BuildId = "1.0", UseWorkerVersioning = true }
        .AddActivity(MyActivities.Greet)
        .AddWorkflow<MyWorkflowV1>());

var v1Handle = await workerV1.ExecuteAsync(async () =>
{
    // Add 1.0 as the default version for the queue
    await client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("1.0"));

    // Start a workflow which will run on the 1.0 worker
    var v1Handle = await client.StartWorkflowAsync(
        (MyWorkflowV1 wf) => wf.RunAsync(),
        new() { Id = "worker-versioning-v1", TaskQueue = taskQueue });

    // Signal the workflow to proceed
    await v1Handle.SignalAsync(wf => wf.ProceederAsync("go"));

    // Give a chance for the workflow to process the signal
    await Task.Delay(1000);

    return v1Handle;
});

// Stop the old worker, add 1.1 as compatible with 1.0, and start a 1.1 worker. We do this to speed along the example,
// since the 1.0 worker may continue to process tasks briefly after we make 1.1 the new default.
await client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewCompatible("1.1", "1.0"));
using var workerV1Dot1 = new TemporalWorker(
    client,
    new TemporalWorkerOptions(taskQueue) { BuildId = "1.1", UseWorkerVersioning = true }
        .AddActivity(MyActivities.Greet)
        .AddActivity(MyActivities.SuperGreet)
        .AddWorkflow<MyWorkflowV1Dot1>());

await workerV1Dot1.ExecuteAsync(async () =>
{
    // Continue driving the workflow. Take note that the new version of the workflow run by the 1.1
    // worker is the one that takes over! You might see a workflow task timeout, if the 1.0 worker is
    // processing a task as the version update happens. That's normal.
    await v1Handle.SignalAsync(wf => wf.ProceederAsync("go"));

    // Add a new *incompatible* version to the task queue, which will become the new overall default for the queue.
    await client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("2.0"));

    // Start a 2.0 worker
    using var workerV2 = new TemporalWorker(
        client,
        new TemporalWorkerOptions(taskQueue) { BuildId = "2.0", UseWorkerVersioning = true }
        .AddActivity(MyActivities.Greet)
        .AddActivity(MyActivities.SuperGreet)
        .AddWorkflow<MyWorkflowV2>());
    await workerV2.ExecuteAsync(async () =>
    {
        // Start a new workflow. Note that it will run on the new 2.0 version, without the client invocation changing
        // at all! Note here we can use `MyWorkflowV1.run` because the signature of the workflow has not changed.
        var v2Handle = await client.StartWorkflowAsync(
            (MyWorkflowV2 wf) => wf.RunAsync(),
            new() { Id = "worker-versioning-v2", TaskQueue = taskQueue });

        // Drive both workflows once more before concluding them. The first workflow will continue running on the 1.1
        // worker.
        await v1Handle.SignalAsync(wf => wf.ProceederAsync("go"));
        await v2Handle.SignalAsync(wf => wf.ProceederAsync("go"));
        await v1Handle.SignalAsync(wf => wf.ProceederAsync("finish"));
        await v2Handle.SignalAsync(wf => wf.ProceederAsync("finish"));

        // Wait for both to complete
        await v1Handle.GetResultAsync();
        await v2Handle.GetResultAsync();

        // Lastly we'll demonstrate how you can use the gRPC api to determine if certain build IDs are ready to be
        // retired. There's more information in the documentation, but here's a quick example that shows us how to
        // tell when the 1.0 worker can be retired:

        // There is a 5 minute buffer before we will consider IDs no longer reachable by new workflows, to
        // account for replication in multi-cluster setups. Uncomment the following line to wait long enough to see
        // the 1.0 worker become unreachable.
        // await Task.Delay(TimeSpan.FromMinutes(5));
        var reachability =
            await client.GetWorkerTaskReachabilityAsync(new List<string> { "2.0", "1.1", "1.0" }, new List<string>());
        if (reachability.BuildIdReachability["1.0"].TaskQueueReachability[taskQueue].Count == 0)
        {
            Console.WriteLine("Build Id 1.0 is no longer reachable");
        }
    });
});