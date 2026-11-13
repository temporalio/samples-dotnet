# Microsoft Agent Framework samples

> Pre-release. The `Temporalio.MicrosoftAgentFramework` package and its interfaces may change before General Availability.

These samples show the [Temporal plugin for Microsoft Agent Framework](https://github.com/temporalio/ai-integrations/tree/main/dotnet/microsoft-agent-framework), which runs an Agent Framework agent inside a Temporal Workflow. You build the agent in Workflow code with `TemporalAgent` and Microsoft.Extensions.AI's `ChatOptions` and tools, and call `RunAsync`. Each model call runs as the `MicrosoftAgentFramework.InvokeModel` Activity with its own timeout and retries. The agent's loop, in-Workflow tools, context providers and approvals run in the Workflow, where Temporal records every step and replays it after a crash or a worker restart. The docs page is [Microsoft Agent Framework](https://docs.temporal.io/develop/dotnet/integrations/microsoft-agent-framework).

`MicrosoftAgentFrameworkPlugin` goes on the client or on the worker, not both. These samples put it on `TemporalClientConnectOptions.Plugins`; the worker built from that client inherits it.

## Samples

| Sample | Description |
|--------|-------------|
| [HelloWorld](HelloWorld) | One `TemporalAgent`, one prompt, one model Activity. Start here. |
| [Tools](Tools) | A plain `AIFunction` that runs in the Workflow next to an Activity wrapped with `ActivityAsTool.Create`. |
| [Mcp](Mcp) | Tools from an MCP server through `TemporalMcpServer.ListToolsAsync`; the listing and each call are Activities. The sample hosts its own server over stdio. |
| [HumanInTheLoop](HumanInTheLoop) | A tool wrapped in `ApprovalRequiredAIFunction`. The Workflow publishes the request through a Query, waits for a `Decide` Update and answers on the same session. |
| [Middleware](Middleware) | Two hooks that run in the Workflow: a `DelegatingAIFunction` that refuses a tool call and an `AIContextProvider` that edits the instructions. |
| [StructuredOutput](StructuredOutput) | `RunAsync<PersonInfo>` sends the record's JSON schema as the response format; the Workflow returns the parsed record. |
| [Streaming](Streaming) | `StreamingTopic` publishes each `ChatResponseUpdate` to a Workflow Stream; the starter prints them as they arrive. |
| [ContinueAsNew](ContinueAsNew) | A chat driven by Updates that hands the serialized session to a fresh run when Temporal suggests Continue-As-New. |

## Prerequisites

1. The .NET 10 SDK.

2. A local Temporal Service from the [Temporal CLI](https://docs.temporal.io/cli):

   ```bash
   temporal server start-dev
   ```

3. An OpenAI API key. Each worker checks for it at start and exits with a one-line message when it is missing, then reads it when it builds the OpenAI chat client; the Workflow only carries the model name `gpt-4o-mini`:

   ```bash
   export OPENAI_API_KEY=...
   ```

## Running a sample

Each sample is one console project with a `worker` verb and a starter verb. Start the worker first, then the starter in a second terminal, both from the sample's directory:

```bash
# Terminal 1
cd src/MicrosoftAgentFramework/HelloWorld
dotnet run worker

# Terminal 2
cd src/MicrosoftAgentFramework/HelloWorld
dotnet run workflow
```

`HumanInTheLoop`, `Streaming`, `ContinueAsNew` and `Mcp` add verbs or arguments of their own; each README lists them.

## Key features

- One Activity per model call, `MicrosoftAgentFramework.InvokeModel`, with a 60-second start-to-close timeout in these samples and the plugin's default 10-second heartbeat timeout. Temporal owns retries, so the workers build the OpenAI client with `ClientRetryPolicy(maxRetries: 0)`; the plugin fails a throwing factory with a non-retryable `MicrosoftAgentFrameworkConfigurationError`.
- Tools run where you put them: a plain `AIFunction` runs in the Workflow, `ActivityAsTool.Create` runs an Activity with its own `StartToCloseTimeout`.
- MCP servers are registered on the worker. `TemporalMcpServer.ListToolsAsync` lists their tools through the `MicrosoftAgentFramework.McpListTools` Activity and each call runs as `MicrosoftAgentFramework.McpCallTool`.
- Approvals are Agent Framework's own `ApprovalRequiredAIFunction`; the wait is a Workflow Update with a validator, and no Activity is open while the Workflow waits.
- Function wrappers and context providers run in the Workflow, so they must be deterministic; I/O in them goes through an Activity.
- Streaming publishes `ChatResponseUpdate`s to a Workflow Stream topic, batched every 100 milliseconds.
- Long chats carry `SerializeSessionAsync` output across Continue-As-New and restore it with `DeserializeSessionAsync`.

## Tests

The tests in [tests/MicrosoftAgentFramework](../../tests/MicrosoftAgentFramework) run every sample against the plugin's `FakeChatClient` and a local dev server, with no API key:

```bash
dotnet test --filter "FullyQualifiedName~TemporalioSamples.Tests.MicrosoftAgentFramework"
```

## Related

- [Temporal plugin for Microsoft Agent Framework](https://github.com/temporalio/ai-integrations/tree/main/dotnet/microsoft-agent-framework)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Microsoft Agent Framework docs page](https://docs.temporal.io/develop/dotnet/integrations/microsoft-agent-framework)
