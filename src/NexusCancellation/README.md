# Nexus Cancellation

This sample demonstrates how to cancel a Nexus operation from a caller workflow. It uses a CancellationType of `WaitCancellationRequested`, which allows the caller workflow to return after the handler workflow has received the cancellation.

### Instructions

To run, first see [README.md](../../README.md) for prerequisites such as starting the Temporal server.

Run the following to create both namespaces and an endpoint:

```
temporal operator namespace create --namespace nexus-cancellation-handler-namespace
temporal operator namespace create --namespace nexus-cancellation-caller-namespace

temporal operator nexus endpoint create \
  --name nexus-cancellation-endpoint \
  --target-namespace nexus-cancellation-handler-namespace \
  --target-task-queue nexus-cancellation-handler-sample
```

In one terminal, run the handler worker from this directory:

```
dotnet run handler-worker
```

In a second terminal, run the caller worker from this directory:

```
dotnet run caller-worker
```

In a third terminal, run the caller workflow from this directory:

```
dotnet run caller-workflow
```

### Output

#### Caller Worker Output

The caller worker output shows when the first operation completes followed by the cancellation of the other operations.

```
...
[22:06:40] info: Program[0]
      Running caller worker
[22:07:16] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      First operation completed, cancelling remaining operations
[22:07:20] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      Operation was cancelled
```

#### Handler Worker Output

The handler worker output shows which operations were canceled.

```
...
[22:07:18] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in Tr was cancelled after 00:00:03 of work, performed 00:00:02 of cleanup
[22:07:19] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in Es was cancelled after 00:00:02 of work, performed 00:00:03 of cleanup
[22:07:19] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in Fr was cancelled after 00:00:03 of work, performed 00:00:03 of cleanup
[22:07:20] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in En was cancelled after 00:00:02 of work, performed 00:00:04 of cleanup
```

#### Workflow Result

The caller workflow output shows the result of the first completed operation.

```
...
[22:07:14] info: Program[0]
      Executing caller hello workflow
[22:07:20] info: Program[0]
      Workflow result: Hallo Temporal 👋
```

#### Note on Timing

This sample waits for all operations to complete using `Workflow.WhenAllAsync(tasks)`. This ensures that all operations have completed (including any cleanup work) before the caller workflow exits.