# OpenTelemetry

This sample shows how to configure OpenTelemetry to capture workflow traces and SDK metrics.

To run, first see [README.md](../../README.md) for prerequisites.

Then, run the following from this directory to start the .NET Aspire Dashboard which will collect telemetry. The dashboard UI is available at http://localhost:18888.

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

Similar to traces, you can select either `worker` or `workflow`.

`worker` will show the metrics emitted by the worker. It may look something like:

![Worker Metrics Screenshot](worker-metrics-screenshot.png)

`workflow` will show the metrics emitted by the client. It may look something like:

![Client Metrics Screenshot](client-metrics-screenshot.png)
