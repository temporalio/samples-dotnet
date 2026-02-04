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
[23:21:04] info: Program[0]
      Running caller worker
[23:21:11] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      First operation completed, cancelling remaining operations
[23:21:11] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      Operation was cancelled
[23:21:11] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      Operation was cancelled
[23:21:11] info: Temporalio.Workflow:HelloCallerWorkflow[0]
      Operation was cancelled
```

#### Handler Worker Output

The handler worker output shows which operations were canceled.

```
[23:20:59] info: Program[0]
      Running handler worker
[23:21:10] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow started for Temporal in Es
[23:21:10] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow started for Temporal in En
[23:21:10] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow started for Temporal in Fr
[23:21:10] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow started for Temporal in Tr
[23:21:10] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow started for Temporal in De
[23:21:14] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in Fr was cancelled after 00:00:04 of work, performed 00:00:03 of cleanup
[23:21:14] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in En was cancelled after 00:00:04 of work, performed 00:00:03 of cleanup
[23:21:14] info: Temporalio.Workflow:HelloHandlerWorkflow[0]
      HelloHandlerWorkflow for Temporal in Es was cancelled after 00:00:04 of work, performed 00:00:03 of cleanup
```

#### Workflow Result

The caller workflow output shows the result of the first completed operation.

```
[23:21:09] info: Program[0]
      Executing caller hello workflow
[23:21:11] info: Program[0]
      Workflow result: Hallo Temporal 👋
```

#### Note on Timing

As this sample uses the CancellationType of `WaitCancellationRequested` you can see that the caller workflow result logs before the cleanup work finishes on the cancelled operations. In the timing above, the caller workflow result logs at `23:21:11`, the cancellations are also logged at `23:21:11` and the handler workflows complete their 3 seconds of cleanup work and log at `23:21:14`.