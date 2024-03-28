# Timer

Use a timer (`Workflow.DelayAsync`) to implement a monthly subscription. Also, handle workflow cancellation.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The worker terminal will show logs from running the workflow.
