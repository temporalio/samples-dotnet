# Mutex

This sample has a MutexWorkflow that receives lock-requested Signals from other Workflows.
It handles the Signals one at a time, first sending a lock-acquired Signal to the sending Workflow,
and then waiting for a Signal from that Workflow indicating that the Workflow is ready to release the lock. Then the MutexWorkflow goes on to the next lock-requested Signal. In this way, you're able to make sure that only one Workflow Execution is performing a certain type of work at a time.

## Structure
This project is divided into two components:
1. The actual sample, featuring user code that demonstrates the use of `WorkflowMutex`, is located in the `TemporalioSamples.Mutex` namespace.
2. The implementation of `WorkflowMutex` resides in the `TemporalioSamples.Mutex.Impl` namespace, highlighting its potential to be abstracted into a separate library.

Below is a brief description of the core files:
* [Impl/WorkflowMutex.cs](Impl/WorkflowMutex.cs): WorkflowMutex is a static class that provides mechanism to acquire a lock for a given resource within a workflow context. The primary functionality is encapsulated in the LockAsync() method, which initiates a lock on a resource and returns a handle. This handle then can be used to release the lock.
* [Impl/MutexWorkflow.workflow.cs](Impl/MutexWorkflow.workflow.cs): The MutexWorkflow class is an internal workflow designed to manage and process a queue of lock requests.
  Each lock request is handled by a dedicated LockHandler class, encapsulating the mechanics of lock handling, including lock acquisition, release, and timeout.
  Requests are processed in the order they are received, and if all have been processed, the workflow waits for new ones. In the event that the workflow has a suggestion to continue as new, all remaining requests will be passed onto the newly created workflow.
* [WorkflowWithMutex.workflow.cs](WorkflowWithMutex.workflow.cs): The WorkflowWithMutex class is an implementation of an asynchronous workflow that acquires a lock on a resource, executes certain activities while the lock is held, and then releases the lock afterward.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will start two workflows, which will both try to acquire the lock at the same time. You should see that one Workflow takes slightly longer than the other, because it needs to wait for lock release.

```
Starting test workflow with id 'test-c30dded9-dbed-454c-b572-25d4d1222067'. Connecting to lock workflow 'locked-resource-id'
Starting test workflow with id 'test-d2a4642b-6bfd-45ee-aba1-321c08e2cb02'. Connecting to lock workflow 'locked-resource-id'
Test workflow 'test-c30dded9-dbed-454c-b572-25d4d1222067' started
Test workflow 'test-d2a4642b-6bfd-45ee-aba1-321c08e2cb02' started
Test workflow 'test-c30dded9-dbed-454c-b572-25d4d1222067' finished after 5506ms
Test workflow 'test-d2a4642b-6bfd-45ee-aba1-321c08e2cb02' finished after 10444ms
```
