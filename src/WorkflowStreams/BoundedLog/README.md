# Bounded log

A ticker Workflow periodically truncates its stream. A fast subscriber sees every tick while a
late subscriber is advanced from a stale offset to the retained base offset.

Each subscriber Signals `SubscriberCompleteAsync` after receiving the final tick. The Workflow
keeps the stream available until `ExpectedSubscribers` distinct subscribers acknowledge completion,
or until a one-minute timeout expires. This allows late subscribers to poll after publishing ends.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the subscribers:

```bash
dotnet run
```
