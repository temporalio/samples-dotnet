namespace TemporalioSamples.LambdaWorker.Worker;

using Temporalio.Worker;

public static class LambdaWorkerSample
{
    public const string TaskQueueEnvironmentVariable = "TEMPORAL_TASK_QUEUE";
    public const string WorkflowIdEnvironmentVariable = "TEMPORAL_LAMBDA_WORKFLOW_ID_PREFIX";
    public const string DeploymentNameEnvironmentVariable = "TEMPORAL_LAMBDA_DEPLOYMENT_NAME";
    public const string BuildIdEnvironmentVariable = "TEMPORAL_LAMBDA_BUILD_ID";

    public const string DefaultTaskQueue = "serverless-task-queue-dotnet";
    public const string DefaultWorkflowId = "serverless-workflow-id-1";
    public const string DefaultDeploymentName = "my-app";
    public const string DefaultBuildId = "build-1";

    public static string TaskQueue =>
        GetEnvironmentVariableOrDefault(TaskQueueEnvironmentVariable, DefaultTaskQueue);

    public static string WorkflowId =>
        GetEnvironmentVariableOrDefault(WorkflowIdEnvironmentVariable, DefaultWorkflowId);

    public static string DeploymentName =>
        GetEnvironmentVariableOrDefault(DeploymentNameEnvironmentVariable, DefaultDeploymentName);

    public static string BuildId =>
        GetEnvironmentVariableOrDefault(BuildIdEnvironmentVariable, DefaultBuildId);

    public static TemporalWorkerOptions ConfigureWorkerOptions(TemporalWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.TaskQueue = TaskQueue;
        return options.
            AddWorkflow<SampleWorkflow>().
            AddActivity(Activities.HelloActivity);
    }

    private static string GetEnvironmentVariableOrDefault(
        string name,
        string defaultValue) =>
        Environment.GetEnvironmentVariable(name) is { } value &&
        !string.IsNullOrWhiteSpace(value) ?
            value :
            defaultValue;
}
