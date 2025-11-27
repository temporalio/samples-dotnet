# Activity Simple

This sample shows a workflow executing a synchronous static activity method and an asynchronous instance activity method.
The Worker program will refresh the Temporal client periodically every 10 seconds. 

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run a workflow every second from this directory:

    watch -n1 dotnet run workflow

This will show logs in the worker window of the workflow running.