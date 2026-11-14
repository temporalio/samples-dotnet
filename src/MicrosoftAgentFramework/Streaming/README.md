# Streaming

Model output streamed to a subscriber outside the Workflow. `TemporalAgentOptions.StreamingTopic = "deltas"` makes the model Activity consume the provider's stream and publish every `ChatResponseUpdate` to the `deltas` topic of a `WorkflowStream` hosted on the Workflow, batched every 100 milliseconds (`StreamingBatchInterval`). The Workflow still gets the whole response, so the durable result is the same as without streaming. One model call is still one `MicrosoftAgentFramework.InvokeModel` Activity.

## What this sample demonstrates

- A `WorkflowStream` field constructed in `[WorkflowInit]`, so its publish Signal, poll Update and offset Query exist before the Activity publishes; a topic on an agent whose Workflow hosts no stream fails the run with `MicrosoftAgentFrameworkWorkflowError` before the first model call
- `StreamingTopic` on `TemporalAgentOptions`; a plain `agent.RunAsync(prompt)` streams once the topic is set
- `WorkflowStreamClient.SubscribeAsync<ChatResponseUpdate>` in the starter, which prints `item.Value.Text` as each update arrives
- A 500-millisecond delay before the Workflow returns, because a closed Workflow cannot be polled and subscribers need time to read the last batch

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run workflow
dotnet run -- "Explain durable execution in one paragraph."
```

`dotnet run workflow` (or `dotnet run` with no arguments) starts the Workflow with the default prompt and subscribes; any other arguments are the prompt. The starter prints the text as it arrives, then `Final result:` with the whole text. The Workflow `microsoft-agent-framework-streaming` shows one `MicrosoftAgentFramework.InvokeModel` Activity and one Signal per published batch.

If the model Activity retries, for example on a provider 5xx, the new attempt publishes from the start again under a new `ResponseId`, so the starter prints a blank line and `(the model Activity retried; starting over)` before the new text. This sample makes one model call, so a new `ResponseId` can only be a retry; an agent with tools gets a new `ResponseId` on every model round, and a subscriber for that agent should key its display on the round as well. The [LlmTokenStreaming](../../WorkflowStreams/LlmTokenStreaming) sample shows the same handling with a dedicated retry topic. `Temporalio.Extensions.WorkflowStreams` is experimental and its wire format may change.

## Files

| File | Description |
|------|-------------|
| `StreamingWorkflow.workflow.cs` | `StreamingInput` and `StreamingWorkflow`, which hosts the stream and sets the topic |
| `Program.cs` | Registers the plugin, then runs the worker or starts the Workflow and subscribes to `deltas`, starting the line over when a retry republishes |
