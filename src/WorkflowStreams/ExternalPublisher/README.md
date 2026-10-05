# External publisher

A Workflow hosts the stream while a normal client publishes news and another client subscribes.
The client signals the Workflow to close after flushing a sentinel event. The long-lived hub also
captures its stream state before Continue-As-New so subscribers can follow the stream across runs.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the publisher and subscriber:

```bash
dotnet run
```
