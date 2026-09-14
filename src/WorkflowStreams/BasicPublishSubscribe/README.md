# Basic publish/subscribe

An order Workflow publishes lifecycle events to `status`, while its payment Activity publishes
progress events to `progress`. The subscriber consumes and decodes both topics.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the subscriber:

```bash
dotnet run
```
