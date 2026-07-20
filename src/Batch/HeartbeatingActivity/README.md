# Heartbeating Activity Batch

A sample implementation of processing a batch by an Activity.

An Activity can run as long as needed. It reports that it is still alive through heartbeat.

If the worker is restarted, the Activity is retried after the heartbeat timeout.

Temporal allows storing data in heartbeat _details_. These details are available to the next
Activity attempt. The progress of the record processing is stored in the details to avoid
reprocessing records from the beginning on failures.

To run, first see [README.md](../../../README.md) for prerequisites. Then, run the following from
this directory in a separate terminal to start the worker. Restart the worker while the batch is
executing to see how the activity timeout and retry work.

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.
