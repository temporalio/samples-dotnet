# Reconnecting subscriber

A subscriber disconnects after two pipeline stages, saves the next offset, and resumes without
gaps or duplicates using a fresh client.

Start a Temporal service, then run the worker:

```bash
dotnet run -- worker
```

In another terminal, run the subscriber:

```bash
dotnet run
```
