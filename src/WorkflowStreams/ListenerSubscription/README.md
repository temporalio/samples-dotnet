# Listener subscription

Starts two order Workflows and subscribes to each with `WorkflowStreamListener`, demonstrating
serialized callbacks and task-based backpressure.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the listener:

```bash
dotnet run
```
