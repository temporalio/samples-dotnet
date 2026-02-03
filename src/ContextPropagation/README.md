# Interceptors

This sample demonstrates how to use interceptors to propagate contextual information from an `AsyncLocal` throughout
workflows, activities, and Nexus operations. While this demonstrates context propagation specifically, it can also be
used to show how to create interceptors for any other purpose.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory in a
separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The workflow terminal will show the completed workflow result and the worker terminal will show the contextual user ID
is present in the workflow, Nexus operation handler, Nexus handler workflow, and activity.