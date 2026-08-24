# Workflow Streams

> **Experimental.** This sample uses `Temporalio.Extensions.WorkflowStreams`, whose API may
> change in future versions.

A workflow stream is a durable, offset-addressed publish/subscribe log hosted inside a Temporal
Workflow. Workflow code and external clients publish to named topics through Signals, subscribers
long-poll through Updates, and a Query exposes the current global offset. The extension handles
batching, publisher deduplication, topic filtering, Continue-As-New handoff, and truncation.

The sample contains six independently linkable scenarios:

- [Basic publish/subscribe](BasicPublishSubscribe)
- [Listener subscription](ListenerSubscription)
- [Reconnecting subscriber](ReconnectingSubscriber)
- [External publisher](ExternalPublisher)
- [Bounded log](BoundedLog)
- [LLM token streaming](LlmTokenStreaming)

The first five commands use the shared worker; LLM streaming uses a separate worker and task queue
because it requires an OpenAI API key.

## Run the sample

First see the [repository README](../../README.md) for prerequisites and start a Temporal service,
for example with `temporal server start-dev`.

Start the shared worker:

```bash
dotnet run -- worker
```

Then run a scenario from another terminal:

```bash
dotnet run -- publisher           # Basic mixed-topic publish/subscribe
dotnet run -- listener            # Listener callbacks and task-based backpressure
dotnet run -- reconnecting        # Disconnect and resume from a saved offset
dotnet run -- external-publisher  # Publish from code outside a Workflow or Activity
dotnet run -- ticker              # Bound the log by truncating old entries
```

### [Basic publish/subscribe](BasicPublishSubscribe)

`OrderWorkflow` publishes lifecycle events to the `status` topic. Its payment Activity creates a
client with `WorkflowStreamClient.FromActivity()` and publishes finer-grained events to `progress`.
The subscriber consumes both topics with `await foreach` and decodes each payload by topic.

### [Listener subscription](ListenerSubscription)

The listener scenario starts two order Workflows and subscribes to each with
`WorkflowStreamListener`. `Subscribe` returns immediately with a
`WorkflowStreamSubscriptionHandle`. Callbacks are serialized per subscription, and the Task
returned by `OnNextAsync` prevents the next item and poll from being delivered until processing
finishes. Unlike a blocking iterator, both this API and .NET's `await foreach` API poll fully
asynchronously without occupying a thread.

### [Reconnecting subscriber](ReconnectingSubscriber)

The first subscriber reads two pipeline stages and saves one past the last global offset it saw.
A fresh client then calls `Subscribe(savedOffset)` and receives the remaining stages without gaps
or duplicates. Offsets are global across topics, not per-topic.

### [External publisher](ExternalPublisher)

`HubWorkflow` only hosts the stream. A normal client publishes news into it while another client
subscribes, then signals the Workflow to close after flushing a sentinel event.

### [Bounded log](BoundedLog)

`TickerWorkflow` periodically calls `Truncate()` to retain only recent events. A fast subscriber
sees every tick. A late subscriber requests an offset that has already been truncated and is
automatically advanced to the retained base offset, demonstrating the bounded-history tradeoff.

## [LLM token streaming](LlmTokenStreaming)

Set `OPENAI_API_KEY` and start the dedicated worker:

```bash
export OPENAI_API_KEY=...
dotnet run -- llm-worker
```

Then run the subscriber with an optional prompt:

```bash
dotnet run -- llm
dotnet run -- llm "Explain durable execution in one paragraph."
```

The Workflow performs no network I/O. Its Activity calls OpenAI with streaming enabled, publishes
token deltas to `delta`, and publishes the final text to `complete`. OpenAI's own retries are
disabled so Temporal owns retry behavior. When an Activity retry starts, it publishes to `retry`;
the subscriber clears the partial terminal output and renders the new attempt from scratch.

To exercise that path, stop the LLM worker while output is streaming and restart it.
