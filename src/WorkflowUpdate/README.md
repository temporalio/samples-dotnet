# Workflow Update

This workflow represents a UI Wizard. We use [Workflow Update](https://docs.temporal.io/workflows#update) 
to mutate the workflow state (submit some data) and wait for the workflow update method to return the next screen 
the client has to navigate to.

The update validator is used to reject null arguments (rejected updates are not included in workflow history).


To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.