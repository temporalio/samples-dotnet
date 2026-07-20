# Sliding Window Batch

A sample implementation of a batch processing Workflow that maintains a sliding window of record
processing Workflows.

A Workflow starts a configured number of child Workflows in parallel. Each child processes a
single record. When a child completes, a new child is immediately started.

A parent Workflow calls continue-as-new after starting a preconfigured number of children. A
child completion is reported through a Signal, as a parent cannot directly wait for a child that
was started by a previous run.

Multiple instances of `SlidingWindowBatchWorkflow` run in parallel, each processing a subset of
records, to support a higher total rate of processing.

This is the sample that demonstrates the workaround for the race between continue-as-new and a
child Workflow's completion Signal: a Signal can be delivered to the new run before its Workflow
run method has had a chance to restore state from the continue-as-new input. See the comments on
`SlidingWindowBatchWorkflow.recordsToRemove` for the details.

The Workflow also continues-as-new early whenever `Workflow.ContinueAsNewSuggested` is true, not
just after starting a fixed `pageSize` number of children. A fixed count alone is fragile: a run
that accumulates a lot of other history (for example, many completion Signals) can outgrow the
suggested history size before it reaches `pageSize` children. See the comments where
`Workflow.ContinueAsNewSuggested` is checked in `SlidingWindowBatchWorkflow.RunAsync` for details.

To run, first see [README.md](../../../README.md) for prerequisites. Then, run the following from
this directory in a separate terminal to start the worker:

    dotnet run worker

Note that `UnhandledCommand` info messages in the worker output are expected and benign. They
ensure that Signals are not lost when there is a race condition between the workflow calling
continue-as-new and receiving a Signal. If these messages appear too frequently, consider
increasing the number of partitions passed to `BatchWorkflow.RunAsync`.

Then in another terminal, run the workflow from this directory. Each time the command runs, it
starts a new `BatchWorkflow` execution with 3 partitions.

    dotnet run workflow

This will show logs in the worker window of the workflow running.
