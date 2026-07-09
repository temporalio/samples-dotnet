namespace TemporalioSamples.Tests.LambdaWorker;

using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Testing;
using Temporalio.Worker;
using TemporalioSamples.LambdaWorker;
using Xunit;
using Xunit.Abstractions;

public class LambdaWorkerTests : TestBase
{
    public LambdaWorkerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [TimeSkippingServerFact]
    public async Task RunAsync_SimpleRun_Succeeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            LambdaWorkerSample.ConfigureWorkerOptions(
                new TemporalWorkerOptions("lambda-worker-test-task-queue")));
        await worker.ExecuteAsync(async () =>
        {
            var result = await env.Client.ExecuteWorkflowAsync(
                (SampleWorkflow wf) => wf.RunAsync("Serverless Lambda Worker!"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Serverless Lambda Worker!!", result);
        });
    }

    [Fact]
    public void ConfigureWorkerOptions_SetsExpectedWorkerRegistration()
    {
        var options = LambdaWorkerSample.ConfigureWorkerOptions(new TemporalWorkerOptions());

        Assert.Equal(LambdaWorkerSample.TaskQueue, options.TaskQueue);
        Assert.Contains(options.Workflows, workflow => workflow.Type == typeof(SampleWorkflow));
        Assert.Contains(
            options.Activities,
            activity => activity.MethodInfo?.DeclaringType == typeof(Activities));
        var workflow = options.Workflows.Single(
            workflow => workflow.Type == typeof(SampleWorkflow));
        Assert.Equal(VersioningBehavior.Pinned, workflow.VersioningBehavior);
    }

    [Fact]
    public void ConfigureWorkerOptions_UsesEnvironmentOverrides()
    {
        using var taskQueue =
            new EnvironmentVariableScope(LambdaWorkerSample.TaskQueueEnvironmentVariable, "fresh-task-queue");
        using var workflowId =
            new EnvironmentVariableScope(LambdaWorkerSample.WorkflowIdEnvironmentVariable, "fresh-workflow");
        using var deploymentName =
            new EnvironmentVariableScope(LambdaWorkerSample.DeploymentNameEnvironmentVariable, "fresh-deployment");
        using var buildId =
            new EnvironmentVariableScope(LambdaWorkerSample.BuildIdEnvironmentVariable, "fresh-build");

        var options = LambdaWorkerSample.ConfigureWorkerOptions(new TemporalWorkerOptions());

        Assert.Equal("fresh-task-queue", options.TaskQueue);
        Assert.Equal("fresh-workflow", LambdaWorkerSample.WorkflowId);
        Assert.Equal("fresh-deployment", LambdaWorkerSample.DeploymentName);
        Assert.Equal("fresh-build", LambdaWorkerSample.BuildId);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        public EnvironmentVariableScope(string name, string value)
        {
            this.name = name;
            originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() =>
            Environment.SetEnvironmentVariable(name, originalValue);
    }
}
