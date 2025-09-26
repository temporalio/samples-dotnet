namespace TemporalioSamples.Tests.WorkerVersioning;

using Temporalio.Client;
using TemporalioSamples.WorkerVersioning;
using Xunit;
using Xunit.Abstractions;

public class WorkerVersioningTests : WorkflowEnvironmentTestBase
{
    public WorkerVersioningTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task WorkerVersioningSampleCanRun()
    {
        var temporalClient = new TemporalClient(Client.Connection, Client.Options);

        using var cts = new CancellationTokenSource();

        var workerV1Task = Task.Run(() => WorkerV1.RunAsync(Client, cts.Token));
        var workerV1_1Task = Task.Run(() => WorkerV1Dot1.RunAsync(Client, cts.Token));
        var workerV2Task = Task.Run(() => WorkerV2.RunAsync(Client, cts.Token));

        try
        {
            await Program.RunDemoAsync(temporalClient);
            Assert.True(true, "Worker versioning demo completed successfully");
        }
        finally
        {
            await cts.CancelAsync();

            try
            {
                await Task.WhenAll(workerV1Task, workerV1_1Task, workerV2Task);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
