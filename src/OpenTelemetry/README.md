# OpenTelemetry

Demonstrates how to use OpenTelemetry tracing and metrics with the .NET SDK. There are two specific samples:

* [CoreSdkForwarding](CoreSdkForwarding) - Shows how to use `OpenTelemetryOptions` to have the internal Core logic
  forward metrics to an OpenTelemetry endpoint.
* [DotNetMetrics](DotNetMetrics) - Shows how to use `Temporalio.Extensions.DiagnosticSource` to forward Core metrics to
  a .NET `Meter` that can then be used with OpenTelemetry.

Both of these samples use the same OpenTelemetry tracing approach but have different metrics approaches.