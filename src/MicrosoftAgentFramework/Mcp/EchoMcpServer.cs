// @@@SNIPSTART dotnet-microsoft-agent-framework-mcp-server
namespace TemporalioSamples.MicrosoftAgentFramework.Mcp;

using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// A one-tool MCP server. `dotnet run mcp-server` serves it over stdio and the worker starts that
// process itself; the test serves the same tools in memory.
public static class EchoMcpServer
{
    public static McpServerTool[] CreateTools() => new[]
    {
        McpServerTool.Create(
            Echo,
            new McpServerToolCreateOptions { Name = "echo", Description = "Returns the message unchanged" }),
    };

    public static string Echo([Description("The message to echo")] string message) => message;

    public static async Task RunAsync()
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in CreateTools())
        {
            tools.Add(tool);
        }

        await using var transport = new StdioServerTransport("echo");
        await using var server = McpServer.Create(
            transport,
            new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "echo", Version = "1.0.0" },
                ToolCollection = tools,
            });
        await server.RunAsync();
    }
}
// @@@SNIPEND
