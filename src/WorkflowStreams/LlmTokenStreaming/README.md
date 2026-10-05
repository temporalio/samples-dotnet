# LLM token streaming

An Activity streams token deltas from OpenAI through a Workflow Stream. Retry events tell the
subscriber to clear partial output and render the new attempt from scratch.

Set `OPENAI_API_KEY`, start a Temporal service, and run the worker:

```bash
export OPENAI_API_KEY=...
dotnet run -- worker
```

In another terminal, run the subscriber with an optional prompt:

```bash
dotnet run
dotnet run -- "Explain durable execution in one paragraph."
```
