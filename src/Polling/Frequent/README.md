# Frequently Polling Activity

This sample shows how we can implement frequent polling (1 second or faster) inside our Activity. The implementation is a loop that polls our service and then sleeps for the poll interval (1 second in the sample).

To ensure that polling Activity is restarted in a timely manner, we make sure that it heartbeats on every iteration. Note that heartbeating only works if we set the `HeartbeatTimeout` to a shorter value than the Activity `StartToCloseTimeout` timeout.

To run, first see [README.md](../../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.