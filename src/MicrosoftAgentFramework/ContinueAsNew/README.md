# Continue-as-new

A chat that runs as long as you like. Each user turn is an Update, so the caller gets the reply from the same call. History grows with every turn, and `Workflow.ContinueAsNewSuggested` turns true once the Temporal Service decides it is large enough. The Workflow waits on that or on the `EndChat` Signal, lets in-flight Updates finish with `Workflow.AllHandlersFinished`, and hands `agent.SerializeSessionAsync(session)` to a fresh run, which restores it with `agent.DeserializeSessionAsync`.

What crosses the boundary: the serialized session, which holds the transcript and Agent Framework's own session state, including an approval request you have not answered yet. What does not: the `TemporalAgent` object, which the new run builds again, MCP connections, and the Workflow's own record of a pending approval. Answer an open approval before continuing, or carry the pending request in your own input; otherwise the first turn of the new run fails with `MicrosoftAgentFrameworkWorkflowError` because the session holds a request with no response.

## What this sample demonstrates

- The `Turn` Update, the `EndChat` Signal and the `Transcript` Query
- A Workflow `Semaphore` so turns take the session one at a time
- Update-With-Start from the starter on the fixed Workflow id `microsoft-agent-framework-chat`, so the first `send-message` starts the chat and later ones join it
- `Workflow.CreateContinueAsNewException` with the serialized session as the next run's input

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run send-message 'Hi! What is durable execution?'
dotnet run send-message 'Give me a one-sentence summary.'
dotnet run end-chat
```

Each `send-message` prints the prompt and the reply. `end-chat` prints the transcript from the Query, signals the Workflow and waits for it to complete. Without a running chat, `end-chat` prints `No chat is running.` and exits with code 1; `send-message` without a prompt prints the usage line and exits with code 1. Two turns do not reach the default history threshold, so the Workflow completes in one run; the test lowers `limit.historyCount.suggestContinueAsNew` to 5 to see the handoff. In a chat application a UI would drive the Updates and the Workflow would run on, continuing as new whenever history gets large.

## Files

| File | Description |
|------|-------------|
| `ChatWorkflow.workflow.cs` | `ChatInput` and `ChatWorkflow` with the Update, Signal and Query |
| `Program.cs` | Registers the plugin, then runs the worker, sends a turn or ends the chat |
