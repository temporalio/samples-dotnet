# MCP

Give the agent the tools of an MCP server. `MicrosoftAgentFrameworkPluginOptions.McpServers` registers the server's transport on the worker under the name `echo`. `TemporalMcpServer.ListToolsAsync("echo", options)` in the Workflow lists the server's tools and returns one `AIFunction` per tool, with the server's input schema, so the model sees the parameters. Each call then runs as an Activity:

| Activity | When it runs |
|----------|--------------|
| `MicrosoftAgentFramework.McpListTools` | Once per `ListToolsAsync`, before the agent runs |
| `MicrosoftAgentFramework.McpCallTool` | Each time the model calls a tool of the server |

The included `EchoMcpServer` is a one-tool server built on the MCP C# SDK. `dotnet run mcp-server` serves it over stdio, and the worker starts this same program with the `mcp-server` command on the first MCP Activity.

## What this sample demonstrates

- `McpServers = { ["echo"] = () => new StdioClientTransport(...) }` on the plugin options
- `TemporalMcpServer.ListToolsAsync` with `McpToolOptions`, a 30-second start-to-close timeout on both the list and the call Activities, and the result passed as `ChatOptions.Tools`
- The server's connection never leaves the worker; the Workflow carries the server name
- Results reach the model the way the MCP C# SDK's `McpClientTool` delivers them: the echo tool's text arrives as a `TextContent`, and a result with `isError` goes back whole so the model reads the server's message; neither fails the Activity
- The model sees the tool as `echo`: the plugin applies the name rule of the Python Agent Framework's `MCPTool` (characters outside `A-Za-z0-9_.-` become `-`), so a tool is named the same under both plugins; `Microsoft.Agents.AI` has no such rule and the MCP C# SDK's `McpClientTool` keeps the server's name, and this name is unchanged either way

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The worker spawns the server itself. The starter prints the agent's answer, and the Workflow `microsoft-agent-framework-mcp` shows one `MicrosoftAgentFramework.McpListTools`, two `MicrosoftAgentFramework.InvokeModel` and one `MicrosoftAgentFramework.McpCallTool` Activity.

## Files

| File | Description |
|------|-------------|
| `EchoMcpServer.cs` | The `echo` tool, served over stdio by the `mcp-server` command and in memory by the test |
| `McpWorkflow.workflow.cs` | `McpWorkflow` with `TemporalMcpServer.ListToolsAsync("echo", ...)` |
| `Program.cs` | Registers the MCP transport and chat client factories, then runs the worker, the starter or the server |
