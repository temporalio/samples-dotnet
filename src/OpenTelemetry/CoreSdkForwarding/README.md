# OpenTelemetry - .Core SDK Forwarding

This sample shows how to configure the SDK to forward metrics and logs from the Core SDK.

The main advantage over using .NET metrics is simplicity.

This sample also shows how to configure custom metrics from both an activity and a workflow in a replay-safe manner.

To run, first see [README.md](../../../README.md) for prerequisites.

Then, run the following from [one directory up ](../docker-compose.yaml) to start the .NET Aspire Dashboard which will collect telemetry. The dashboard UI is available at http://localhost:18888.

    docker compose up

Then, run the following from this directory in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The workflow will complete.

## Traces

Traces can be viewed at http://localhost:18888/traces.

You can select either `worker` or `workflow` for traces; both should show the same trace. The workflow should appear and when clicked, may look something like:

![Tracing Screenshot](tracing-screenshot.png)

## Metrics

Metrics can be viewed by clicking the metrics tab on the dashboard.

Select `temporal-core-sdk`.

All metrics emitted by the Core SDK will be shown. It may look something like:

![Metrics Screenshot](metrics-screenshot.png)

## Logs

The Core SDK writes its own logs to the console, where `ILogger` never sees them. Setting `Forwarding` on
`LoggingOptions` routes them to the given logger instead:

```csharp
Logging = new LoggingOptions()
{
    // Core SDK logs default to WARN for Temporal's crates, ERROR for everything else.
    Filter = new TelemetryFilterOptions(core: TelemetryFilterOptions.Level.Info),
    Forwarding = new LogForwardingOptions(loggerFactory.CreateLogger("Temporalio.Core")),
}
```

Forwarded logs are prefixed with their Core SDK target:

    [10:45:27] info: Temporalio.Core[0]
          [sdk_core::temporalio_sdk_core::worker] Initializing worker namespace="default", task_queue="opentelemetry-sample-core-sdk-forwarding"

Since the logger factory here also has an OpenTelemetry provider, they show up on the dashboard's structured logs tab.
