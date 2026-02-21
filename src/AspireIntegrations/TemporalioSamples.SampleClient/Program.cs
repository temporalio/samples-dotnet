using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using TemporalioSamples.SampleWorkflow;

try
{
    var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
    Console.WriteLine("\nAttempting to connect client to temporal server...");

    var client = await TemporalClient.ConnectAsync(connectOptions);
    Console.WriteLine("✅ Client connected successfully!");

    await client.StartWorkflowAsync(
        (SimpleWorkflow wf) => wf.RunAsync(),
        new(id: "simple-workflow-id", taskQueue: "simple-task-queue"));

    Console.WriteLine("✅ Workflow invoked successfully!");
}
#pragma warning disable CA1031
catch (Exception ex)
#pragma warning restore CA1031
{
    Console.WriteLine($"❌ Failed to start workflow: {ex.Message}");
}