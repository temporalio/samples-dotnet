# Standalone Activity

This sample shows how to execute Activities directly from a Temporal Client, without a Workflow.

For full documentation, see [Standalone Activities](https://docs.temporal.io/develop/dotnet/standalone-activities).

### Sample directory structure

- [MyActivities.cs](MyActivities.cs) - Activity definition with `[Activity]` attribute
- [Program.cs](Program.cs) - Worker, execute, start, list, and count commands

### Steps to run this sample

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, execute a standalone activity and wait for the result:

    dotnet run execute-activity

Or start a standalone activity, get a handle, then wait for the result:

    dotnet run start-activity

List standalone activity executions:

    dotnet run list-activities

Count standalone activity executions:

    dotnet run count-activities

Note: `list-activities` and `count-activities` are only available in the
[Standalone Activity prerelease CLI](https://github.com/temporalio/cli/releases/tag/v1.6.2-standalone-activity).
