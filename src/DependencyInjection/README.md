# Dependency Injection

This sample shows an activity having a dependency injected and a worker running as a generic host with an injected
`ITemporalClient`.

It also uses `LogForwardingOptions` to forward logs from the internal Core SDK, which are otherwise written to the
console and never seen by `ILogger`, to the host's injected logger factory.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory
in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

This will show logs in the worker window of the workflow running.