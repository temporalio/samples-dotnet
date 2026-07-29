# Nexus Simple

This sample demonstrates how to author a Nexus service and call it from a Workflow. It intentionally
contrasts synchronous and asynchronous Nexus operations:

* `Echo` uses `OperationHandler.Sync` because it is a short, bounded, in-memory operation with no
  external calls or side effects.
* `SayHello` is backed by a Workflow that waits on a durable 15-second timer before returning. The
  delay intentionally exceeds the 10-second synchronous Nexus request deadline.

Use `OperationHandler.Sync` only for highly reliable, low-latency, bounded operations that complete
well within that short request window. Use an asynchronous operation when latency or 
availability is uncertain, the work might exceed the handler deadline, or execution depends on a potentially
unreliable service or database.

An asynchronous handler must still initiate or attach to the underlying work and return an operation
token within the request deadline. The work can continue afterward, and the caller retrieves its
eventual result through the operation handle. In this sample, that work is a Workflow.

## Instructions

To run, first see [README.md](../../README.md) for prerequisites such as starting the Temporal server.

Run the following to create both namespaces and an endpoint:

```
temporal operator namespace create --namespace nexus-simple-handler-namespace
temporal operator namespace create --namespace nexus-simple-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-simple-endpoint \
  --target-namespace nexus-simple-handler-namespace \
  --target-task-queue nexus-simple-handler-sample \
  --description-file endpoint_description.md
```

In one terminal, run the handler worker from this directory:

```
dotnet run handler-worker
```

In a second terminal, run the caller worker from this directory:

```
dotnet run caller-worker
```

In a third terminal, run the caller workflow:

```
dotnet run caller-workflow
```

The Echo result returns inline. For SayHello, the caller worker logs that the asynchronous operation
started, waits approximately 15 seconds, and then logs its completion.
