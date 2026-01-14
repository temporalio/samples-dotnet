using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Common.EnvConfig;

namespace TemporalioSamples.WorkerVersioning;

public static class Program
{
    public const string TaskQueue = "worker-versioning";
    public const string DeploymentName = "my-deployment";

    private static readonly string[] DoActivitySignal = { "do-activity" };
    private static readonly string[] SomeSignal = { "some-signal" };
    private static readonly string[] ConcludeSignal = { "conclude" };

    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: WorkerVersioning <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  worker-v1    - Start worker with version 1.0");
            Console.WriteLine("  worker-v1.1  - Start worker with version 1.1");
            Console.WriteLine("  worker-v2    - Start worker with version 2.0");
            Console.WriteLine("  demo         - Run the complete versioning demonstration");
            return;
        }

        var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
        connectOptions.TargetHost ??= "localhost:7233";
        var client = await TemporalClient.ConnectAsync(connectOptions);

        switch (args[0].ToLower())
        {
            case "worker-v1":
                await WorkerV1.RunAsync(client);
                break;
            case "worker-v1.1":
                await WorkerV1Dot1.RunAsync(client);
                break;
            case "worker-v2":
                await WorkerV2.RunAsync(client);
                break;
            case "demo":
                await RunDemoAsync(client);
                break;
            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                break;
        }
    }

    public static async Task RunDemoAsync(TemporalClient client)
    {
        // Wait for v1 worker and set as current version
        var workerV1Version = new WorkerDeploymentVersion(Program.DeploymentName, "1.0");
        Console.WriteLine("Waiting for v1 worker to appear. Run `dotnet run worker-v1` in another terminal");
        await WaitForWorkerVersionAsync(client, workerV1Version);
        await SetCurrentVersionAsync(client, workerV1Version);

        // Start auto-upgrading and pinned workflows. Importantly, note that when we start the workflows,
        // we are using a workflow type name which does *not* include the version number. We defined them
        // with versioned names so we could show changes to the code, but here when the client invokes
        // them, we're demonstrating that the client remains version-agnostic.
        var autoUpgradingId = $"worker-versioning-versioning-autoupgrade_{Guid.NewGuid()}";
        var autoUpgradingHandle = await client.StartWorkflowAsync(
            "AutoUpgradingWorkflow",
            Array.Empty<object>(),
            new(id: autoUpgradingId, taskQueue: Program.TaskQueue));
        Console.WriteLine($"Started auto-upgrading workflow: {autoUpgradingHandle.Id}");

        var pinnedId = $"worker-versioning-versioning-pinned_{Guid.NewGuid()}";
        var pinnedHandle = await client.StartWorkflowAsync(
            "PinnedWorkflow",
            Array.Empty<object>(),
            new(id: pinnedId, taskQueue: Program.TaskQueue));
        Console.WriteLine($"Started pinned workflow: {pinnedHandle.Id}");

        // Signal both workflows a few times to drive them
        await SignalWorkflowsAsync(autoUpgradingHandle, pinnedHandle);

        // Now wait for the v1.1 worker to appear and become current
        var workerV1_1Version = new WorkerDeploymentVersion(Program.DeploymentName, "1.1");
        Console.WriteLine("Waiting for v1.1 worker to appear. Run `dotnet run worker-v1.1` in another terminal");
        await WaitForWorkerVersionAsync(client, workerV1_1Version);
        await SetCurrentVersionAsync(client, workerV1_1Version);

        // Once it has, we will continue to advance the workflows.
        // The auto-upgrade workflow will now make progress on the new worker, while the pinned one will
        // keep progressing on the old worker.
        await SignalWorkflowsAsync(autoUpgradingHandle, pinnedHandle);

        // Finally we'll start the v2 worker, and again it'll become the new current version
        var workerV2Version = new WorkerDeploymentVersion(Program.DeploymentName, "2.0");
        Console.WriteLine("Waiting for v2 worker to appear. Run `dotnet run worker-v2` in another terminal");
        await WaitForWorkerVersionAsync(client, workerV2Version);
        await SetCurrentVersionAsync(client, workerV2Version);

        // Once it has we'll start one more new workflow, another pinned one, to demonstrate that new
        // pinned workflows start on the current version.
        var pinnedV2Id = $"worker-versioning-versioning-pinned-2_{Guid.NewGuid()}";
        var pinnedV2Handle = await client.StartWorkflowAsync(
            "PinnedWorkflow",
            Array.Empty<object>(),
            new(id: pinnedV2Id, taskQueue: Program.TaskQueue));
        Console.WriteLine($"Started pinned workflow v2: {pinnedV2Handle.Id}");

        // Now we'll conclude all workflows. You should be able to see in your server UI that the pinned
        // workflow always stayed on 1.0, while the auto-upgrading workflow migrated.
        foreach (var handle in new[] { autoUpgradingHandle, pinnedHandle, pinnedV2Handle })
        {
            await handle.SignalAsync("DoNextSignal", ConcludeSignal);
            await handle.GetResultAsync();
        }

        Console.WriteLine("All workflows completed");
    }

    /// <summary>Signal both workflows a few times to drive them.</summary>
    private static async Task SignalWorkflowsAsync(WorkflowHandle autoUpgradingHandle, WorkflowHandle pinnedHandle)
    {
        await autoUpgradingHandle.SignalAsync("DoNextSignal", DoActivitySignal);
        await pinnedHandle.SignalAsync("DoNextSignal", SomeSignal);
    }

    private static async Task WaitForWorkerVersionAsync(TemporalClient client, WorkerDeploymentVersion version)
    {
        while (true)
        {
            try
            {
                var request = new DescribeWorkerDeploymentRequest
                {
                    Namespace = client.Options.Namespace,
                    DeploymentName = version.DeploymentName,
                };

                var response = await client.WorkflowService.DescribeWorkerDeploymentAsync(request);

                var versionInfo = response.WorkerDeploymentInfo.VersionSummaries
                    .FirstOrDefault(v => v.DeploymentVersion?.BuildId == version.BuildId);

                if (versionInfo != null)
                {
                    return;
                }
            }
            catch (Temporalio.Exceptions.RpcException)
            {
                // Deployment not found yet
            }

            await Task.Delay(1000);
        }
    }

    private static async Task SetCurrentVersionAsync(TemporalClient client, WorkerDeploymentVersion version)
    {
        // First get the current deployment info for the conflict token
        var describeRequest = new DescribeWorkerDeploymentRequest
        {
            Namespace = client.Options.Namespace,
            DeploymentName = version.DeploymentName,
        };

        var describeResponse = await client.WorkflowService.DescribeWorkerDeploymentAsync(describeRequest);

        // Set the current version
        var setRequest = new SetWorkerDeploymentCurrentVersionRequest
        {
            Namespace = client.Options.Namespace,
            DeploymentName = version.DeploymentName,
            BuildId = version.BuildId,
            ConflictToken = describeResponse.ConflictToken,
        };

        await client.WorkflowService.SetWorkerDeploymentCurrentVersionAsync(setRequest);
    }
}
