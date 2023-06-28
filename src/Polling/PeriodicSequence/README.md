# Periodic Polling of a Sequence of Activities

This sample demonstrates how to use a Child Workflow for periodic Activity polling.

This is a rare scenario where polling requires execution of a Sequence of Activities, or Activity arguments need to change between polling retries. For this case we use a Child Workflow to call polling activities a set number of times in a loop and then periodically call Continue-As-New.

To run, first see [README.md](../../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.