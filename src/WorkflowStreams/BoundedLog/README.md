# Bounded log

A ticker Workflow periodically truncates its stream. A fast subscriber sees every tick while a
late subscriber is advanced from a stale offset to the retained base offset.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the subscribers:

```bash
dotnet run
```
