# OpenTelemetry - .NET Metrics

This sample shows how to configure the SDK to forward metrics from the Core SDK.

The main advantage over using .NET metrics is simplicity; additionally, there is no need to take a dependency on the .NET OpenTelemetry libraries.

Note that in order to set up tracing, .NET OpenTelemetry must be used. See the [DotNetMetrics](../DotNetMetrics) sample for an example of how to set up tracing.

This sample also shows how to configure custom metrics from both an activity and a workflow in a replay-safe manner.

To run, first see [README.md](../../../README.md) for prerequisites.

Then, run the following from [one directory up ](../docker-compose.yaml) to start the .NET Aspire Dashboard which will collect telemetry. The dashboard UI is available at http://localhost:18888.

    docker compose up

Then, run the following from this directory in a separate terminal to start the worker:

    dotnet run worker

Then in another terminal, run the workflow from this directory:

    dotnet run workflow

The workflow will complete.

## Metrics

Metrics can be viewed by clicking the metrics tab on the dashboard.

Select `temporal-core-sdk`.

All metrics emitted by the Core SDK will be shown. It may look something like:

![Metrics Screenshot](metrics-screenshot.png)
