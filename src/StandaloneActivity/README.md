# Standalone Activity

This sample shows how to execute Activities directly from a Temporal Client, without a Workflow.

For full documentation, see [Standalone Activities](https://docs.temporal.io/develop/dotnet/standalone-activities).

**Note: Temporal CLI support for Standalone Activities requires CLI version 1.9.0.** See setup guide: https://docs.temporal.io/cli/setup-cli

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
