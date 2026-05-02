using Temporalio.Extensions.ToolRegistry;
using TemporalioSamples.ToolRegistryIncidentTriage;
using Xunit;

namespace TemporalioSamples.ToolRegistryIncidentTriage.Tests;

/// <summary>
/// Unit tests for BuildTriageRegistry. Drives the registry directly via
/// Dispatch — bypasses RunWithSessionAsync (which requires an activity context)
/// and the LLM provider. Mirrors the TS / Python / Go / Ruby suites.
/// </summary>
public class TriageActivityTest
{
    private static AlertPayload MakeAlert() => new(
        Status: "firing",
        Labels: new() { ["alertname"] = "HighLatencyP99", ["service"] = "api", ["runbook"] = "rollback-or-scale" },
        Annotations: new() { ["summary"] = "P99 > 1s", ["description"] = "P99 above threshold for 1m." },
        StartsAt: DateTime.UtcNow.ToString("o"));

    private static TriageActivity.TriageDeps MakeDeps(
        Func<AlertPayload, ApprovalRequest, Task<ApprovalResponse>>? approve = null,
        Func<string, string, IReadOnlyDictionary<string, object?>, Task<string>>? mcpCall = null,
        Func<string, Task<(string, string)>>? exec = null) =>
        new()
        {
            McpListTools = baseUrl => Task.FromResult(
                baseUrl.Contains("7071")
                    ? new List<TriageActivity.McpToolInfo>
                    {
                        new("prometheus_query", "instant PromQL query",
                            new Dictionary<string, object?>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object?>
                                {
                                    ["query"] = new Dictionary<string, object?> { ["type"] = "string" },
                                },
                                ["required"] = new[] { "query" },
                            }),
                    }
                    : new List<TriageActivity.McpToolInfo>
                    {
                        new("kubectl_describe", "describe a k8s resource",
                            new Dictionary<string, object?>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object?>
                                {
                                    ["resource"] = new Dictionary<string, object?> { ["type"] = "string" },
                                    ["name"] = new Dictionary<string, object?> { ["type"] = "string" },
                                    ["namespace"] = new Dictionary<string, object?> { ["type"] = "string" },
                                },
                                ["required"] = new[] { "resource", "name" },
                            }),
                    }),
            McpCallTool = mcpCall ?? ((url, name, args) => Task.FromResult($"(mocked {name})")),
            RequestHumanApproval = approve ?? ((alert, req) => Task.FromResult(new ApprovalResponse("approved", "default-mock"))),
            ExecShellCommand = exec ?? (cmd => Task.FromResult(($"(mocked exec: {cmd})", ""))),
        };

    private record Call(string Name, IReadOnlyDictionary<string, object?> Input);

    private static (TriageResult? result, IList<IDictionary<string, object?>> sessionResults) Drive(
        TriageActivity.TriageDeps deps, params Call[] calls)
    {
        var session = new AgenticSession();
        var (registry, getResult) = TriageActivity.BuildTriageRegistry(MakeAlert(), session, deps);
        foreach (var c in calls)
        {
            registry.Dispatch(c.Name, c.Input);
        }
        return (getResult(), session.Results);
    }

    [Fact]
    public void HappyPathResolved()
    {
        int approvalCalls = 0;
        var deps = MakeDeps(approve: (a, r) =>
        {
            approvalCalls++;
            return Task.FromResult(new ApprovalResponse("approved", "go ahead"));
        });
        var action = "kubectl rollout restart deploy/api -n demo-app";

        var (result, sessionResults) = Drive(deps,
            new Call("prometheus_query", new Dictionary<string, object?> { ["query"] = "up{service='api'}" }),
            new Call("kubectl_describe", new Dictionary<string, object?> { ["resource"] = "pod", ["name"] = "api-xyz", ["namespace"] = "demo-app" }),
            new Call("propose_remediation", new Dictionary<string, object?> { ["action"] = action, ["justification"] = "leak; restart reclaims memory" }),
            new Call("request_human_approval", new Dictionary<string, object?>
            {
                ["message"] = "Restart api?",
                ["diagnosis"] = "memory leak",
                ["proposedAction"] = action,
            }),
            new Call("execute_remediation", new Dictionary<string, object?> { ["action"] = action }),
            new Call("report_resolved", new Dictionary<string, object?> { ["summary"] = "restarted; latency normal" }));

        Assert.NotNull(result);
        Assert.Equal("resolved", result!.Status);
        Assert.Contains("restart", result.Summary);
        Assert.Single(result.Remediations);
        Assert.Equal(action, result.Remediations[0].Action);
        Assert.Equal(1, approvalCalls);
        Assert.Equal(new[] { "remediation", "approval", "executed", "final" },
            sessionResults.Select(r => (string)r["kind"]!));
    }

    [Fact]
    public void RejectedApprovalUnresolved()
    {
        var deps = MakeDeps(approve: (a, r) => Task.FromResult(new ApprovalResponse("rejected", "off-hours; defer until tomorrow")));

        var (result, sessionResults) = Drive(deps,
            new Call("propose_remediation", new Dictionary<string, object?> { ["action"] = "kubectl scale ...", ["justification"] = "transient" }),
            new Call("request_human_approval", new Dictionary<string, object?>
            {
                ["message"] = "Scale?",
                ["diagnosis"] = "transient",
                ["proposedAction"] = "kubectl scale ...",
            }),
            new Call("report_unresolved", new Dictionary<string, object?> { ["summary"] = "operator deferred" }));

        Assert.Equal("unresolved", result!.Status);
        Assert.Contains("deferred", result.Summary);
        var approval = sessionResults.FirstOrDefault(r => (string)r["kind"]! == "approval");
        Assert.NotNull(approval);
        Assert.Equal("rejected", approval!["decision"]);
        Assert.Contains("off-hours", (string)approval["reason"]!);
    }

    [Fact]
    public void ExecuteRefusesWithoutApproval()
    {
        string? executedCmd = null;
        var deps = MakeDeps(exec: cmd => { executedCmd = cmd; return Task.FromResult(("ran", "")); });

        var (result, _) = Drive(deps,
            new Call("execute_remediation", new Dictionary<string, object?> { ["action"] = "rm -rf /" }),
            new Call("report_unresolved", new Dictionary<string, object?> { ["summary"] = "tried to skip approval" }));

        Assert.Equal("unresolved", result!.Status);
        Assert.Null(executedCmd);
    }

    [Fact]
    public void ExecuteRefusesWhenActionDoesNotMatch()
    {
        string? executedCmd = null;
        var deps = MakeDeps(
            approve: (a, r) => Task.FromResult(new ApprovalResponse("approved", "ok")),
            exec: cmd => { executedCmd = cmd; return Task.FromResult(("ran", "")); });

        var (result, _) = Drive(deps,
            new Call("propose_remediation", new Dictionary<string, object?> { ["action"] = "kubectl restart api", ["justification"] = "x" }),
            new Call("request_human_approval", new Dictionary<string, object?>
            {
                ["message"] = "Restart?",
                ["diagnosis"] = "x",
                ["proposedAction"] = "kubectl restart api",
            }),
            new Call("execute_remediation", new Dictionary<string, object?> { ["action"] = "kubectl scale deploy/api --replicas=10" }),
            new Call("report_unresolved", new Dictionary<string, object?> { ["summary"] = "guard tripped" }));

        Assert.Equal("unresolved", result!.Status);
        Assert.Null(executedCmd);
    }

    [Fact]
    public void McpToolsRegistered()
    {
        var deps = MakeDeps();
        var session = new AgenticSession();
        var (registry, _) = TriageActivity.BuildTriageRegistry(MakeAlert(), session, deps);
        var names = registry.Definitions.Select(d => d.Name).ToList();
        foreach (var want in new[]
        {
            "prometheus_query", "kubectl_describe",
            "propose_remediation", "request_human_approval",
            "execute_remediation", "report_resolved", "report_unresolved",
        })
        {
            Assert.Contains(want, names);
        }
    }

    [Fact]
    public void McpDispatchForwardsToSidecar()
    {
        var calls = new List<(string Url, string Name, IReadOnlyDictionary<string, object?> Args)>();
        var deps = MakeDeps(mcpCall: (url, name, args) =>
        {
            calls.Add((url, name, args));
            return Task.FromResult($"result for {name}");
        });

        Drive(deps,
            new Call("prometheus_query", new Dictionary<string, object?> { ["query"] = "up{}" }),
            new Call("report_unresolved", new Dictionary<string, object?> { ["summary"] = "test" }));

        Assert.Single(calls);
        Assert.Equal("prometheus_query", calls[0].Name);
        Assert.Contains("7071", calls[0].Url);
        Assert.Equal("up{}", calls[0].Args["query"]);
    }
}
