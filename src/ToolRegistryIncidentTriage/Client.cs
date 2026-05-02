// Standalone client CLI (dotnet run --project Client.csproj is convenient,
// but for the demo this lives as a static method on the worker assembly,
// invoked via `dotnet run -- <args>` after switching the project's OutputType
// or via `dotnet TriageWorker.dll client <args>` once published.
//
// Listing pending approval workflows: use the Temporal CLI directly.
using Temporalio.Client;
using TemporalioSamples.ToolRegistryIncidentTriage;

namespace TemporalioSamples.ToolRegistryIncidentTriage;

public static class ClientCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: client <approve|reject|trigger> ...");
            return 1;
        }

        var client = await MakeClientAsync().ConfigureAwait(false);
        var cmd = args[0];
        var rest = args.Skip(1).ToArray();

        return cmd switch
        {
            "approve" when rest.Length >= 2 => await DecideAsync(client, "approved", rest[0], string.Join(" ", rest.Skip(1))),
            "reject" when rest.Length >= 2 => await DecideAsync(client, "rejected", rest[0], string.Join(" ", rest.Skip(1))),
            "trigger" when rest.Length >= 2 => await TriggerAsync(client, rest[0], rest[1]),
            _ => Usage(cmd),
        };
    }

    private static int Usage(string cmd)
    {
        Console.Error.WriteLine($"Unknown or malformed command: {cmd}");
        Console.Error.WriteLine("Usage: client <approve|reject|trigger> ...");
        return 1;
    }

    private static async Task<TemporalClient> MakeClientAsync()
    {
        var address = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? throw new InvalidOperationException("TEMPORAL_ADDRESS missing");
        var ns = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? throw new InvalidOperationException("TEMPORAL_NAMESPACE missing");
        var apiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY") ?? throw new InvalidOperationException("TEMPORAL_API_KEY missing");
        return await TemporalClient.ConnectAsync(new()
        {
            TargetHost = address,
            Namespace = ns,
            ApiKey = apiKey,
            Tls = new() { },
        });
    }

    private static async Task<int> DecideAsync(TemporalClient client, string decision, string workflowId, string reason)
    {
        var handle = client.GetWorkflowHandle<ApprovalWorkflow>(workflowId);
        await handle.SignalAsync(wf => wf.ApprovalDecisionAsync(new ApprovalResponse(decision, reason)));
        Console.WriteLine($"signaled {workflowId}: {decision} — {reason}");
        return 0;
    }

    private static async Task<int> TriggerAsync(TemporalClient client, string alertname, string service)
    {
        var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "triage-dotnet";
        var wfId = $"triage-{alertname.ToLowerInvariant()}-{service.ToLowerInvariant()}";
        var alert = new AlertPayload(
            Status: "firing",
            Labels: new() { ["alertname"] = alertname, ["service"] = service, ["severity"] = "critical", ["runbook"] = "synthetic" },
            Annotations: new()
            {
                ["summary"] = $"Synthetic test alert for {service}",
                ["description"] = "Triggered manually via Client to exercise the triage flow.",
            },
            StartsAt: DateTime.UtcNow.ToString("o"));
        var handle = await client.StartWorkflowAsync(
            (IncidentTriageWorkflow w) => w.RunAsync(alert),
            new(id: wfId, taskQueue: taskQueue));
        Console.WriteLine($"started triage workflow: {handle.Id} on {taskQueue}");
        return 0;
    }
}
