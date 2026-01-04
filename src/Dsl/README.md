# DSL

This sample demonstrates a Temporal workflow that interprets and executes arbitrary workflow steps defined in a
YAML-based Domain Specific Language (DSL).

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run a workflow from this directory:

    dotnet run workflow workflow1.yaml

Or run the more complex parallel workflow:

    dotnet run workflow workflow2.yaml

The worker terminal will show logs of activities being executed, and the workflow terminal will display the final
variables after the workflow completes.
