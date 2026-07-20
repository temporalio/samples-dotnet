# Iterator Batch

A sample implementation of the Workflow iterator pattern.

A Workflow starts a configured number of child Workflows in parallel. Each child processes a
single record. After all children complete, the parent calls continue-as-new and starts the
children for the next page of records.

This allows processing a set of records of any size. The advantage of this approach is
simplicity. The main disadvantage is that it processes records in batches, with each batch
waiting for the slowest child Workflow.

A variation of this pattern runs Activities instead of child Workflows.

To run, first see [README.md](../../../README.md) for prerequisites. Then, run the following from
this directory in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.
