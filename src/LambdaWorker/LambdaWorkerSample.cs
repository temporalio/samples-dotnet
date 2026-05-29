namespace TemporalioSamples.LambdaWorker;

using Temporalio.Worker;

public static class LambdaWorkerSample
{
    public const string TaskQueue = "serverless-task-queue-dotnet";
    public const string WorkflowId = "serverless-workflow-id-1";
    public const string DeploymentName = "my-app";
    public const string BuildId = "build-1";

    public static TemporalWorkerOptions ConfigureWorkerOptions(TemporalWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.TaskQueue = TaskQueue;
        return options.
            AddWorkflow<SampleWorkflow>().
            AddActivity(Activities.HelloActivity);
    }
}
