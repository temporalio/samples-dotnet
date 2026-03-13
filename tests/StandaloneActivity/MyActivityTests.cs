namespace TemporalioSamples.Tests.StandaloneActivity;

using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.StandaloneActivity;
using Xunit;
using Xunit.Abstractions;

public class MyActivityTests : TestBase
{
    public MyActivityTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact(Skip = "Standalone Activity is not yet supported by WorkflowEnvironment.StartTimeSkippingAsync")]
    public async Task ExecuteActivityAsync_SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("my-task-queue").
                AddActivity(MyActivities.ComposeGreetingAsync));
        await worker.ExecuteAsync(async () =>
        {
            var result = await env.Client.ExecuteActivityAsync(
                () => MyActivities.ComposeGreetingAsync(new ComposeGreetingInput("Hello", "World")),
                new($"act-{Guid.NewGuid()}", worker.Options.TaskQueue!)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
                });
            Assert.Equal("Hello, World!", result);
        });
    }
}
