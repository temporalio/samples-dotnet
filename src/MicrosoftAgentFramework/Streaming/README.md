# Streaming

Model output streamed to a subscriber outside the Workflow. `TemporalAgentOptions.StreamingTopic = "deltas"` makes the model Activity consume the provider's stream and publish every `ChatResponseUpdate` to the `deltas` topic of a `WorkflowStream` hosted on the Workflow, batched every 100 milliseconds (`StreamingBatchInterval`). The Workflow still gets the whole response, so the durable result is the same as without streaming. One model call is still one `MicrosoftAgentFramework.InvokeModel` Activity.

## What this sample demonstrates

- A `WorkflowStream` field constructed in `[WorkflowInit]`, so its publish Signal, poll Update and offset Query exist before the Activity publishes
- `StreamingTopic` on `TemporalAgentOptions`; a plain `agent.RunAsync(prompt)` streams once the topic is set
- `WorkflowStreamClient.SubscribeAsync<ChatResponseUpdate>` in the starter, which prints `item.Value.Text` as each update arrives
- A 500-millisecond delay before the Workflow returns, because a closed Workflow cannot be polled and subscribers need time to read the last batch

## Running the sample

```bash
# Terminal 1
dotnet run worker

# Terminal 2
dotnet run
dotnet run -- "Explain durable execution in one paragraph."
```

The default verb starts the Workflow and subscribes; any arguments are the prompt. The starter prints the text as it arrives, then `Final result:` with the whole text. The Workflow `microsoft-agent-framework-streaming` shows one `MicrosoftAgentFramework.InvokeModel` Activity and one Signal per published batch. `Temporalio.Extensions.WorkflowStreams` is experimental and its wire format may change.

## Files

| File | Description |
|------|-------------|
| `StreamingWorkflow.workflow.cs` | `StreamingInput` and `StreamingWorkflow`, which hosts the stream and sets the topic |
| `Program.cs` | Registers the plugin, then runs the worker or starts the Workflow and subscribes to `deltas` |
