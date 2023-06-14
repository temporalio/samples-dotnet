# Schedules

This sample shows how to schedule Workflows to be run at specific times in the future.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

In this sample, a reminder Workflow is scheduled to run every 10 seconds with:

    dotnet run schedule-start

The reminder Workflow will run and log from the Worker every 10 seconds.

You can now run:

    dotnet run schedule-go-faster
    dotnet run schedule-pause
    dotnet run schedule-unpause
    dotnet run schedule-delete

