# Safe Message Handlers

This sample shows a workflow using `Temporalio.Workflows.Semaphore` to atomically process certain blocks of workflow
code to prevent data race issues. The sample code demonstrates assigning cluster nodes to jobs atomically.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running and assigning nodes to jobs. To see what this looks
like with a continue-as-new operation to relieve history pressure, pass `--test-continue-as-new` to
`dotnet run workflow`.