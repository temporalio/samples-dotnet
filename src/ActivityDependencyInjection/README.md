# Activity Dependency Injection

This sample shows how to use helpers and extensions to support instantiating activity classes with dependency injection.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will run the workflow and on the worker terminal you will see these lines:

    Singleton activity DB call: some-db-value from table some-db-table
    Transient activity DB call: some-db-value from table some-db-table