# Middleware

Two hooks that run in the Workflow, next to the agent's loop. `BlockOutsideTmp` is a `DelegatingAIFunction` around `delete_file` that refuses paths outside `/tmp/` by returning a result without calling the inner function, so the tool never runs and the model reads the refusal. `AppendReminder` is an `AIContextProvider` that adds one sentence to the instructions of every model call.

Agent Framework's `AIAgentBuilder` middleware (`agent.AsBuilder().Use(...)`) wraps `AIAgent.RunAsync`, which the plugin does not support inside a Workflow: that entry point resumes off the Workflow scheduler. Wrapping the tool and adding a context provider are the hooks that stay on it. Both run inside the Workflow, so a Workflow Definition that uses them must stay deterministic: no clock, no I/O, no randomness. A hook that needs I/O calls an Activity and adds the result to the prompt.

## What this sample demonstrates

- `Tools = { new BlockOutsideTmp(deleteFile) }` and `AIContextProviders = { new AppendReminder() }` on `TemporalAgentOptions`
- Refusing a tool call from a function wrapper without an Activity
- Adding to the instructions from a context provider before the request leaves for the model Activity; the plugin appends the provider's text to `ChatOptions.Instructions`

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
```

The prompt asks for two deletes. The agent reports that `/etc/passwd` was refused and `/tmp/scratch.txt` was deleted, in one short sentence because of the reminder. The Workflow `microsoft-agent-framework-middleware` shows three `MicrosoftAgentFramework.InvokeModel` Activities and nothing for the tool calls.

## Files

| File | Description |
|------|-------------|
| `BlockOutsideTmp.cs` | The `DelegatingAIFunction` that refuses paths outside `/tmp/` |
| `AppendReminder.cs` | The `AIContextProvider` that edits the instructions |
| `MiddlewareWorkflow.workflow.cs` | `MiddlewareWorkflow` with the wrapped tool and the provider |
| `Program.cs` | Registers the plugin, then runs the worker or the starter |
