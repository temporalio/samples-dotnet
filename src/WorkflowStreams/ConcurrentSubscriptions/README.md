# Concurrent subscriptions

Runs two order Workflows and consumes each reusable `IAsyncEnumerable<WorkflowStreamItem>`
concurrently. Awaiting each item provides natural backpressure within each subscription.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the subscribers:

```bash
dotnet run
```
