using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace TemporalioSamples.WorkerVersioning;

public static class WorkerV1Dot1
{
    public static async Task RunAsync(ITemporalClient client, CancellationToken cancellationToken = default)
    {
        var deploymentVersion = new WorkerDeploymentVersion(Program.DeploymentName, "1.1");

        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(Program.TaskQueue)
            {
                DeploymentOptions = new WorkerDeploymentOptions
                {
                    Version = deploymentVersion,
                    UseWorkerVersioning = true,
                    DefaultVersioningBehavior = VersioningBehavior.AutoUpgrade,
                },
            }
            .AddWorkflow<AutoUpgradingWorkflowV1Dot1>()
            .AddWorkflow<PinnedWorkflowV1>()
            .AddActivity(new MyActivities().SomeActivity)
            .AddActivity(new MyActivities().SomeIncompatibleActivity));

        Console.WriteLine($"Starting worker with version: {deploymentVersion}");
        await worker.ExecuteAsync(cancellationToken);
    }
}
