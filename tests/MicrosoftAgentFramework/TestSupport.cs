namespace TemporalioSamples.Tests.MicrosoftAgentFramework;

using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Temporalio.Client;
using Temporalio.MicrosoftAgentFramework;
using Temporalio.MicrosoftAgentFramework.Testing;
using Temporalio.Worker;

internal static class TestSupport
{
    // The samples name this model; the tests register the plugin's FakeChatClient under it.
    public const string Model = "gpt-4o-mini";

    public const string InvokeModel = "MicrosoftAgentFramework.InvokeModel";

    public const string McpListTools = "MicrosoftAgentFramework.McpListTools";

    public const string McpCallTool = "MicrosoftAgentFramework.McpCallTool";

    // Every Workflow gets a run timeout, so a stalled run fails the test instead of hanging CI.
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    public static MicrosoftAgentFrameworkPlugin PluginFor(
        FakeChatClient fake, string? mcpServer = null, IClientTransport? mcpTransport = null)
    {
        var mcpServers = new Dictionary<string, Func<IClientTransport>>();
        if (mcpServer is not null && mcpTransport is not null)
        {
            mcpServers[mcpServer] = () => mcpTransport;
        }

        return new(new MicrosoftAgentFrameworkPluginOptions
        {
            ChatClients = new Dictionary<string, Func<IChatClient>> { [Model] = () => fake },
            McpServers = mcpServers,
        });
    }

    // The plugin goes on the worker options here and nowhere else, so env.Client stays untouched.
    public static TemporalWorkerOptions WorkerOptions(string scenario, MicrosoftAgentFrameworkPlugin plugin) =>
        new($"microsoft-agent-framework-{scenario}-{Guid.NewGuid()}") { Plugins = new ITemporalWorkerPlugin[] { plugin } };

    public static WorkflowOptions WorkflowOptions(TemporalWorker worker, string scenario) =>
        new(id: $"microsoft-agent-framework-{scenario}-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
        {
            RunTimeout = RunTimeout,
        };

    public static async Task<IReadOnlyDictionary<string, int>> ActivityCountsAsync(WorkflowHandle handle)
    {
        var history = await handle.FetchHistoryAsync();
        return history.Events.
            Where(evt => evt.ActivityTaskScheduledEventAttributes is not null).
            GroupBy(evt => evt.ActivityTaskScheduledEventAttributes!.ActivityType.Name, StringComparer.Ordinal).
            ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    public static IReadOnlyList<FunctionResultContent> ToolResults(FakeChatClientCall call) =>
        call.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToList();
}
