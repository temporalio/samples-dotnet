# Batch

These samples show three different best practices for processing a batch of records with a
Temporal Workflow, each trading off simplicity, efficiency, and failure handling differently.

1. [Iterator](./Iterator/README.md) - Processes a page of records at a time using child
   workflows, continuing-as-new between pages.
2. [Heartbeating Activity](./HeartbeatingActivity/README.md) - Processes the whole batch inside a
   single heartbeating Activity.
3. [Sliding Window](./SlidingWindow/README.md) - Keeps a fixed number of record processing child
   workflows running in parallel at all times, using Signals and continue-as-new to keep the
   Workflow history size bounded indefinitely.

These are .NET ports of the
[Java batch samples](https://github.com/temporalio/samples-java/tree/main/core/src/main/java/io/temporal/samples/batch),
kept as close as possible to the original Java structure and naming.
