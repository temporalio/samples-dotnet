using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;
using TemporalioSamples.ActivityWorker;

// Create a client to localhost on default namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Create worker
using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(taskQueue: "activity-worker-sample").
        AddActivity(SayHelloActivities.SayHello));

// Run worker until our code finishes
await worker.ExecuteAsync(async () =>
{
    // Run the workflow from Go. Since this is just a sample we will run the worker and the workflow
    // client here in the same process, but usually these are done separately.
    var result = await client.ExecuteWorkflowAsync(
        (ISayHelloWorkflow wf) => wf.RunAsync("Temporal"),
        new() { Id = "activity-worker-sample-workflow-id", TaskQueue = "activity-worker-sample" });

    Console.WriteLine("Workflow result: {0}", result);
});

namespace TemporalioSamples.ActivityWorker
{
    public static class SayHelloActivities
    {
        // Our activity implementation
        [Activity("say-hello-activity")]
        public static string SayHello(string name) => $"Hello, {name}!";
    }

    // Workflow definition of the workflow implementation in Go
    [Workflow("say-hello-workflow")]
    public interface ISayHelloWorkflow
    {
        [WorkflowRun]
        Task<string> RunAsync(string name);
    }
}