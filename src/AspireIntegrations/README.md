# Temporal Extensions for .NET Aspire

## Overview

This project provides custom Aspire resource definitions that enable developers to integrate Temporal workflow servers into their Aspire applications with minimal configuration. It supports three deployment models:

- **Local Testing** - Temporal server using `Temporalio.Testing.WorkflowEnvironment` for fast local development and testing
- **Container-based** - Docker container running the official Temporal server image for development and staging environments
- **CLI-based** - Temporal CLI server for environments where Docker isn't available

### Key Features

- ✅ **Service Discovery** - Automatic environment variable injection for dependent services
- ✅ **Health Checks** - Built-in health checks integrated into Aspire's health pipeline
- ✅ **Resource Management** - Start/Stop commands in the Aspire dashboard with proper state management

## Prerequisites

### Required
- **.NET 10.0** or later
- **Aspire 13.0** or later

### For Container-based Setup
- **Docker** - Required to run the Temporal container
- **Docker image** - `temporalio/temporal:latest` (automatically pulled)

### For CLI-based Setup
- **Temporal CLI** - Install via [Temporal CLI documentation](https://docs.temporal.io/cli/install)

## Quick Start

### Local Server Setup

The local server setup uses a Temporal environment for fast testing without external dependencies. It will download and run the necessary Temporal server binaries.

**AppHost.cs:**
```csharp
using Temporal.Extensions.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Temporal local server
var temporal = builder.AddTemporalLocalTestServer();

// Add a worker project that depends on Temporal
builder.AddProject<Projects.SampleWorker>("worker")
    .WaitFor(temporal)
    .WithReference(temporal);

builder.Build().Run();
```

**Advanced Configuration:**
```csharp
var temporal = builder.AddTemporalLocalTestServer(configure: options =>
{
    // Port configuration
    options.UIPort = 8233;                  // Web UI port
    options.MetricsPort = 9233;             // Metrics endpoint port
    
    // Network binding
    options.TargetHost = "0.0.0.0:7233";

    // Namespace configuration
    options.Namespace = "default";
    options.AdditionalNamespaces = ["orders", "analytics"];

    // Use existing Temporal server binary
    options.DevServerOptions.ExistingPath = "/usr/local/bin/temporal";

    // UI configuration
    options.UI = true;

    // Search attributes for custom workflows
    options.SearchAttributes = new[]
    {
        new SearchAttribute { Name = "Environment", ValueType = "Text" },
        new SearchAttribute { Name = "UserId", ValueType = "Text" },
        new SearchAttribute { Name = "ProcessingTime", ValueType = "Int" }
    };
    
    // Dynamic configuration values
    options.DynamicConfigValues = [
        "persistence.cassandra.hosts = cassandra-host:9042"
    ];
});
```

> Use `ExistingPath` to leverage a pre-installed Temporal binary

---

### Container-based Setup

Deploy Temporal using the CLI Docker container.

**AppHost.cs:**
```csharp
using Temporal.Extensions.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Temporal as a Docker container
var temporal = builder.AddTemporalDevContainer(
    configure: options =>
    {
        options.ImageTag = "latest";  // Use specific version if needed
        options.UI = true;             // Enable Web UI
        options.Namespace = "default";
    });

// Add dependent projects that require Temporal
builder.AddProject<Projects.SampleWorker>("worker")
    .WaitFor(temporal)
    .WithReference(temporal);

builder.Build().Run();
```

**Advanced Configuration:**
```csharp
var temporal = builder.AddTemporalDevContainer(configure: options =>
{
    // Network configuration
    options.TargetHost = "127.0.0.1:7233";

    // Namespaces
    options.AdditionalNamespaces = ["default", "custom-ns"];

    // Logging
    options.DevServerOptions.LogLevel = "info";
    options.DevServerOptions.LogFormat = "json";

    // Search attributes for custom workflows
    options.SearchAttributes = new[]
    {
        new SearchAttribute { Name = "CustomField", ValueType = "Text" },
        new SearchAttribute { Name = "CustomInt", ValueType = "Int" }
    };
});
```
---

### CLI-based Setup

Use the Temporal CLI server for environments without Docker.

**AppHost.cs:**
```csharp
using Temporal.Extensions.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add Temporal CLI server
var temporal = builder.AddTemporalCliServer(
    configure: options =>
    {
        options.Namespace = "default";
        options.UI = true;
    });

// Add dependent projects that require Temporal
builder.AddProject<Projects.SampleWorker>("worker")
    .WaitFor(temporal)
    .WithReference(temporal);

builder.Build().Run();
```

**Requirements:**
- Temporal CLI must be installed and available in PATH
- Run `temporal --version` to verify installation

---

### Connection Strings

Dependent projects can access Temporal connection information via environment variables:

```csharp
var temporalAddress = Environment.GetEnvironmentVariable("TEMPORAL_ADDRESS");
var temporalUiAddress = Environment.GetEnvironmentVariable("TEMPORAL_UI_ADDRESS");
```
---

## Configuration Options

### TemporalResourceOptions

The base configuration class for all resource types:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Namespace` | string | "default" | Primary namespace for workflows |
| `AdditionalNamespaces` | List<string> | ["default"] | Additional namespaces to register |
| `Port` | int | 7233 | gRPC service port |
| `UIPort` | int | 8233 | Web UI port |
| `MetricsPort` | int | 9233 | Metrics endpoint port |
| `UI` | bool | true | Enable Web UI |
| `TargetHost` | string | "0.0.0.0:7233" | Bind address (IP:port format) |
| `SearchAttributes` | List<SearchAttribute> | null | Custom search attributes |
| `DynamicConfigValues` | List<string> | [] | Dynamic configuration |
| `CodecEndpoint` | string | null | Codec server endpoint |
| `CodecAuth` | string | null | Codec authentication token |

### Container-specific Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ImageTag` | string | "latest" | Docker image tag version |

---

## Resources

- [Temporal Documentation](https://docs.temporal.io/)
- [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire)
- [Aspire Integrations](https://learn.microsoft.com/dotnet/aspire/integrations)
