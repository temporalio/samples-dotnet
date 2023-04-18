# Activity Worker

This sample shows a Go workflow calling a .NET activity.

To run, first see [README.md](../../README.md) for prerequisites. Then, with [Go](https://go.dev/) installed, run the
following from the [go-workflow] directory in a separate terminal:

    go run .

Then in another terminal, run the sample from this directory:

    dotnet run

The .NET code will invoke the Go workflow which will execute the .NET activity and return. The output should be:

```
Workflow result: Hello, Temporal!
```