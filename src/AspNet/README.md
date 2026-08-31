# ASP.NET Sample

This sample shows how to create a generic host worker for a workflow and an ASP.NET web application that starts it.

The worker also uses `LogForwardingOptions` to forward logs from the internal Core SDK, which are otherwise written to
the console and never seen by `ILogger`, to the host's injected logger factory.

To run, first see [README.md](../../README.md) for prerequisites. Then, start the worker by running the following in the
[Worker](Worker) directory:

    dotnet run

This will start the worker. Now to start the web application, run the following from the [Web](Web) directory:

    dotnet run

This will make a URL available that will run a workflow. Navigating to http://localhost:5000 will output
"Hello, Temporal!". Navigating to http://localhost:5000?name=John will output "Hello, John!".