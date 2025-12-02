# Refreshing Client

This sample demonstrates how to periodically refresh the Temporal client in a Worker. 
The Worker program refreshes the Temporal client every 2 hours, which is useful for scenarios requiring credential mTLS or api key rotation. 

`ClientRefreshAsync` accepts a Func to deliver a new client, to replace the callers Worker client.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run a workflow every second from this directory:

    watch -n1 dotnet run workflow

This will show logs in the worker window of the workflow running.