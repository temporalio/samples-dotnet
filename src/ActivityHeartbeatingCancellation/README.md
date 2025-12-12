# Activity Heartbeating and Cancellation

This sample demonstrates:

- How a retried Activity Task can resume from the last Activity Task's heartbeat.
- How to handle canceling a long-running Activity when its associated Workflow is canceled.

Docs: [Activity heartbeating](https://docs.temporal.io/activities#activity-heartbeat) and [Cancellation](https://docs.temporal.io/activities#cancellation)

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.

# Retry + Heartbeat Activity Example

This example demonstrates how to combine **Retry Policies** and **Activity Heartbeats**
in Temporal's .NET SDK.

## What This Sample Shows
- How to configure retry settings for an activity.
- How an activity can send heartbeat progress.
- How Temporal detects stalled activities via `HeartbeatTimeout`.

This pattern is useful for:
- Batch processing
- Migration jobs
- Long-running operations
- Resumable workflows

## How to Run

1. Start Temporal locally (`temporalite start`).
2. Run the worker:
