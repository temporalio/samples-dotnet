# Workflow Streams

> **Experimental.** This sample uses `Temporalio.Extensions.WorkflowStreams`, whose API may
> change in future versions.

A workflow stream is a durable, offset-addressed publish/subscribe log hosted inside a Temporal
Workflow. Workflow code and external clients publish to named topics through Signals, subscribers
long-poll through Updates, and a Query exposes the current global offset. The extension handles
batching, publisher deduplication, topic filtering, Continue-As-New handoff, and truncation.

Each scenario is a self-contained project with its own worker, client, models, constants, and
instructions:

- [Basic publish/subscribe](BasicPublishSubscribe)
- [Concurrent subscriptions](ConcurrentSubscriptions)
- [Reconnecting subscriber](ReconnectingSubscriber)
- [External publisher](ExternalPublisher)
- [Bounded log](BoundedLog)
- [LLM token streaming](LlmTokenStreaming)

See the [repository README](../../README.md) for common prerequisites. Each scenario README has
the commands for running that project.
