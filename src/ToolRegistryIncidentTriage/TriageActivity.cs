using System.Diagnostics;
using System.Text.Json;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Extensions.ToolRegistry;
using Temporalio.Extensions.ToolRegistry.Providers;

namespace TemporalioSamples.ToolRegistryIncidentTriage;

/// <summary>
/// .NET port of the triage activity. Mirrors workers/typescript, workers/python,
/// workers/go, workers/ruby.
///
/// Structure: <see cref="BuildTriageRegistry"/> returns the (registry, getResult) pair.
/// Pure modulo TriageDeps. <see cref="TriageIncidentAsync"/> composes
/// AgenticSession.RunWithSessionAsync + the registry + Anthropic provider.
/// </summary>
public class TriageActivity
{
    public const string SystemPrompt = """
        You are an SRE on-call agent triaging a production alert.

        You have these tools (sourced from MCP sidecars + per-language helpers):
          - prometheus_query(query)            instant PromQL query
          - prometheus_query_range(query, start, end, step)
          - prometheus_alerts()                what is currently firing
          - kubectl_get(resource, namespace?)  list K8s resources
          - kubectl_describe(resource, name, namespace?)
          - kubectl_logs(pod, namespace, tail?)
          - propose_remediation(action, justification)   record but do NOT execute
          - request_human_approval(message, diagnosis, proposedAction)
                                               blocks until operator says approve|reject
          - execute_remediation(action)        ONLY callable AFTER approval was approved.
                                               Pass the same action you got approved.
          - report_resolved(summary)           ends the loop with status=resolved
          - report_unresolved(summary)         ends the loop with status=unresolved

        Workflow:
          1. Read the alert. Use prometheus_query to confirm the symptom is currently true.
          2. Use kubectl_get/describe/logs and prometheus_query_range to find root cause.
          3. propose_remediation with a specific action (e.g., "kubectl rollout restart deploy/api -n demo-app").
          4. request_human_approval, attaching your diagnosis and the proposed action.
          5. If approved: execute_remediation, then prometheus_query to verify the symptom is gone, then report_resolved.
          6. If rejected: report_unresolved with the operator's reason.

        Be terse. Conversation history is heartbeated to Temporal — keep tool inputs short.
        """;

    /// <summary>Pluggable I/O for the activity. Tests substitute their own.</summary>
    public class TriageDeps
    {
        public required Func<string, Task<List<McpToolInfo>>> McpListTools { get; init; }
        public required Func<string, string, IReadOnlyDictionary<string, object?>, Task<string>> McpCallTool { get; init; }
        public required Func<AlertPayload, ApprovalRequest, Task<ApprovalResponse>> RequestHumanApproval { get; init; }
        public required Func<string, Task<(string Stdout, string Stderr)>> ExecShellCommand { get; init; }
    }

    public record McpToolInfo(string Name, string Description, IReadOnlyDictionary<string, object?> InputSchema);

    public static TriageDeps DefaultDeps() => new()
    {
        McpListTools = DefaultMcpListToolsAsync,
        McpCallTool = DefaultMcpCallToolAsync,
        RequestHumanApproval = RealRequestHumanApprovalAsync,
        ExecShellCommand = DefaultExecShellCommandAsync,
    };

    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static async Task<List<McpToolInfo>> DefaultMcpListToolsAsync(string baseUrl)
    {
        var body = await McpRpcAsync(baseUrl, "tools/list", null).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        var result = new List<McpToolInfo>();
        if (!doc.RootElement.TryGetProperty("result", out var res)) return result;
        if (!res.TryGetProperty("tools", out var tools)) return result;
        foreach (var t in tools.EnumerateArray())
        {
            var name = t.GetProperty("name").GetString() ?? "";
            var desc = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            IReadOnlyDictionary<string, object?> schema = t.TryGetProperty("inputSchema", out var s)
                ? JsonElementToDict(s)
                : new Dictionary<string, object?> { ["type"] = "object" };
            result.Add(new McpToolInfo(name, desc, schema));
        }
        return result;
    }

    private static async Task<string> DefaultMcpCallToolAsync(string baseUrl, string name, IReadOnlyDictionary<string, object?> args)
    {
        var body = await McpRpcAsync(baseUrl, "tools/call", new Dictionary<string, object?>
        {
            ["name"] = name,
            ["arguments"] = args,
        }).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("error", out var err))
        {
            return $"MCP error: {err.GetProperty("message").GetString()}";
        }
        if (!doc.RootElement.TryGetProperty("result", out var res)) return "";
        if (!res.TryGetProperty("content", out var content)) return "";
        var parts = new List<string>();
        foreach (var c in content.EnumerateArray())
        {
            if (c.TryGetProperty("text", out var text))
            {
                parts.Add(text.GetString() ?? "");
            }
        }
        return string.Join("\n", parts);
    }

    private static async Task<string> McpRpcAsync(string baseUrl, string method, object? @params)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["method"] = method,
        };
        if (@params != null) payload["params"] = @params;
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(baseUrl, content).ConfigureAwait(false);
        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, object?> JsonElementToDict(JsonElement el)
    {
        var d = new Dictionary<string, object?>();
        if (el.ValueKind != JsonValueKind.Object) return d;
        foreach (var p in el.EnumerateObject())
        {
            d[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Object => JsonElementToDict(p.Value),
                JsonValueKind.Array => p.Value.EnumerateArray().Select(e => e.ToString()).ToList(),
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => p.Value.ToString(),
            };
        }
        return d;
    }

    private static async Task<(string, string)> DefaultExecShellCommandAsync(string cmd)
    {
        var psi = new ProcessStartInfo("sh", $"-c \"{cmd}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        var completed = await Task.WhenAny(Task.Run(() => p.WaitForExit()), Task.Delay(60_000)).ConfigureAwait(false);
        if (completed is Task<bool> b && !b.Result)
        {
            try { p.Kill(); } catch { }
            throw new TimeoutException($"exec timed out: {cmd}");
        }
        return (await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }

    /// <summary>
    /// Build the tool registry plus a getter for the final result. Pure modulo deps.
    /// Note: handlers are sync (Func&lt;..., string&gt;) per the .NET SDK signature, so
    /// async I/O inside a handler uses .GetAwaiter().GetResult() — sync-over-async.
    /// (See PROPOSAL_FEEDBACK.md item on SDK async-handler support.)
    /// </summary>
    public static (ToolRegistry Registry, Func<TriageResult?> GetResult) BuildTriageRegistry(
        AlertPayload alert, AgenticSession session, TriageDeps deps)
    {
        var registry = new ToolRegistry();
        var promMcp = Environment.GetEnvironmentVariable("MCP_PROMETHEUS_URL") ?? "http://localhost:7071/";
        var k8sMcp = Environment.GetEnvironmentVariable("MCP_KUBERNETES_URL") ?? "http://localhost:7072/";

        RegisterMcpTools(registry, promMcp, deps);
        RegisterMcpTools(registry, k8sMcp, deps);

        var remediations = new List<ProposedRemediation>();
        string? approvedAction = null;
        TriageResult? final = null;

        registry.Register(
            new ToolDef(
                Name: "propose_remediation",
                Description: "Record a remediation you would apply. Does NOT execute it.",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["action"] = new Dictionary<string, object?> { ["type"] = "string" },
                        ["justification"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "action", "justification" },
                }),
            input =>
            {
                var r = new ProposedRemediation((string)input["action"]!, (string)input["justification"]!);
                remediations.Add(r);
                session.Results.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "remediation",
                    ["action"] = r.Action,
                    ["justification"] = r.Justification,
                });
                return "recorded";
            });

        registry.Register(
            new ToolDef(
                Name: "request_human_approval",
                Description: "Block until operator decides. Returns JSON {decision, reason}.",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["message"] = new Dictionary<string, object?> { ["type"] = "string" },
                        ["diagnosis"] = new Dictionary<string, object?> { ["type"] = "string" },
                        ["proposedAction"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "message", "diagnosis", "proposedAction" },
                }),
            input =>
            {
                var req = new ApprovalRequest(
                    (string)input["message"]!,
                    (string)input["diagnosis"]!,
                    (string)input["proposedAction"]!);
                var resp = deps.RequestHumanApproval(alert, req).GetAwaiter().GetResult();
                if (resp.Decision == "approved") approvedAction = req.ProposedAction;
                session.Results.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "approval",
                    ["decision"] = resp.Decision,
                    ["reason"] = resp.Reason,
                });
                return JsonSerializer.Serialize(new { decision = resp.Decision, reason = resp.Reason });
            });

        registry.Register(
            new ToolDef(
                Name: "execute_remediation",
                Description: "Execute the previously-approved action. Errors if no approval has been granted.",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["action"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "action" },
                }),
            input =>
            {
                var action = (string)input["action"]!;
                if (approvedAction == null)
                    return "ERROR: no approval has been granted. Call request_human_approval first.";
                if (action != approvedAction)
                    return $"ERROR: requested action does not match approved action. Approved: {approvedAction}";
                var (stdout, stderr) = deps.ExecShellCommand(action).GetAwaiter().GetResult();
                session.Results.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "executed",
                    ["action"] = action,
                    ["stdout"] = Clip(stdout, 2000),
                    ["stderr"] = Clip(stderr, 2000),
                });
                var output = !string.IsNullOrEmpty(stdout) ? stdout : (!string.IsNullOrEmpty(stderr) ? stderr : "ok");
                return Clip(output, 4000);
            });

        registry.Register(
            new ToolDef(
                Name: "report_resolved",
                Description: "Ends the loop with status=resolved.",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["summary"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "summary" },
                }),
            input =>
            {
                final = new TriageResult("resolved", (string)input["summary"]!, new(remediations));
                session.Results.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "final",
                    ["status"] = final.Status,
                    ["summary"] = final.Summary,
                });
                return "ok";
            });

        registry.Register(
            new ToolDef(
                Name: "report_unresolved",
                Description: "Ends the loop with status=unresolved.",
                InputSchema: new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["summary"] = new Dictionary<string, object?> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "summary" },
                }),
            input =>
            {
                final = new TriageResult("unresolved", (string)input["summary"]!, new(remediations));
                session.Results.Add(new Dictionary<string, object?>
                {
                    ["kind"] = "final",
                    ["status"] = final.Status,
                    ["summary"] = final.Summary,
                });
                return "ok";
            });

        return (registry, () => final);
    }

    private static void RegisterMcpTools(ToolRegistry registry, string baseUrl, TriageDeps deps)
    {
        try
        {
            var tools = deps.McpListTools(baseUrl).GetAwaiter().GetResult();
            foreach (var t in tools)
            {
                var name = t.Name;
                registry.Register(
                    new ToolDef(name, t.Description, t.InputSchema),
                    input => deps.McpCallTool(baseUrl, name, input).GetAwaiter().GetResult());
            }
        }
        catch
        {
            // MCP server unreachable during testing or boot — proceed without these tools.
        }
    }

    public static string BuildPrompt(AlertPayload alert)
    {
        string Get(Dictionary<string, string> m, string k, string d) =>
            m.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v : d;

        return $"Alert fired: {Get(alert.Labels, "alertname", "unknown")} on {Get(alert.Labels, "service", "unknown")}.\n"
            + $"Summary: {Get(alert.Annotations, "summary", "(none)")}\n"
            + $"Description: {Get(alert.Annotations, "description", "(none)")}\n"
            + $"Runbook hint: {Get(alert.Labels, "runbook", "(none)")}\n\n"
            + "Investigate, propose, get approval, and either fix or report unresolved.";
    }

    private static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n);

    [Activity("triage_incident_activity")]
    public async Task<TriageResult> TriageIncidentAsync(AlertPayload alert)
    {
        var deps = DefaultDeps();
        return await AgenticSession.RunWithSessionAsync(async session =>
        {
            var (registry, getResult) = BuildTriageRegistry(alert, session, deps);
            var provider = new AnthropicProvider(
                new AnthropicConfig { ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") },
                registry,
                SystemPrompt);
            await session.RunToolLoopAsync(provider, registry, BuildPrompt(alert)).ConfigureAwait(false);
            var final = getResult();
            if (final == null)
            {
                throw new InvalidOperationException("Agent ended the loop without calling report_resolved or report_unresolved");
            }
            return final;
        }).ConfigureAwait(false);
    }

    private static async Task<ApprovalResponse> RealRequestHumanApprovalAsync(AlertPayload alert, ApprovalRequest req)
    {
        var address = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS") ?? throw new InvalidOperationException("TEMPORAL_ADDRESS missing");
        var ns = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? throw new InvalidOperationException("TEMPORAL_NAMESPACE missing");
        var apiKey = Environment.GetEnvironmentVariable("TEMPORAL_API_KEY") ?? throw new InvalidOperationException("TEMPORAL_API_KEY missing");
        var taskQueue = Environment.GetEnvironmentVariable("TEMPORAL_TASK_QUEUE") ?? "triage-dotnet";

        var client = await TemporalClient.ConnectAsync(new()
        {
            TargetHost = address,
            Namespace = ns,
            ApiKey = apiKey,
            Tls = new() { },
        });

        var key = $"{(alert.Labels.GetValueOrDefault("alertname", "unknown")).ToLowerInvariant()}-{(alert.Labels.GetValueOrDefault("service", "unknown")).ToLowerInvariant()}";
        var wfId = $"approval-{key}";

        var handle = await client.StartWorkflowAsync(
            (ApprovalWorkflow w) => w.RunAsync(key),
            new(id: wfId, taskQueue: taskQueue)
            {
                StartSignal = "approval-request",
                StartSignalArgs = new[] { (object)req },
                // If the activity retries while the approval workflow is still running,
                // attach to the existing one rather than starting a new approval. The
                // operator should not get a second prompt for the same incident.
                IdConflictPolicy = Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy.UseExisting,
            });
        return await handle.GetResultAsync();
    }
}
