# Updatable Timer Sample

Demonstrates a helper class which relies on `Workflow.WaitConditionAsync` to implement a blocking sleep that can be updated at
any moment.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

Check the output of the worker window. The expected output is:

    Running worker
    [14:47:03] info: Temporalio.Workflow:MyWorkflow[0]
        Sleep until: 12/01/2025 01:47:03 +00:00
    [14:47:03] info: Temporalio.Workflow:MyWorkflow[0]
        Going to sleep for 23:59:59.9706583

Then run the updater as many times as you want to change timer to 10 seconds from now:

    dotnet run update-timer    

Check the output of the worker window. The expected output is:

    [...]
    [14:50:50] info: Temporalio.Workflow:MyWorkflow[0]
        Update wake up time: 11/30/2025 01:51:00 +00:00
    [14:50:50] info: Temporalio.Workflow:MyWorkflow[0]
        Going to sleep for 00:00:09.9794955
    [14:51:00] info: Temporalio.Workflow:MyWorkflow[0]
        Sleep completed