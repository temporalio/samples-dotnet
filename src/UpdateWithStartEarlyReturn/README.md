# Update with Start - Early Return

This sample demonstrates how to start a workflow and wait for only part of it to complete, letting the rest of the
workflow run in the background. This is demonstrated with a simple payment processing workflow that returns after
authorization while continuing the rest of the payment processing in the background.

To run, first see [README.md](../../README.md) for prerequisites. Then, run the following from this directory in a
separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The workflow terminal will output something like:

```
Starting payment processing and waiting for authorize
Payment authorized, can move on while rest of payment processing finishes...
Payment processing complete
```

See the code for more details.