namespace TemporalioSamples.Tests.Gcp.CloudRun;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.Gcp.CloudRun;
using Xunit;
using Xunit.Abstractions;

public class GreetingWorkflowTests : TestBase
{
    public GreetingWorkflowTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_Greets()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("cloud-run-worker").
                AddWorkflow<GreetingWorkflow>().
                AddActivity(GreetingActivities.SayHello));
        await worker.ExecuteAsync(async () =>
        {
            var result = await env.Client.ExecuteWorkflowAsync(
                (GreetingWorkflow wf) => wf.RunAsync("Temporal"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }
}
