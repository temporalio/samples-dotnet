namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Constants used for Temporal resource configuration and endpoints.
/// </summary>
public static class TemporalResourceConstants
{
    /// <summary>The name of the gRPC service endpoint.</summary>
    public const string ServiceEndpointName = "grpc";

    /// <summary>The default port for the gRPC service endpoint.</summary>
    public const int DefaultServiceEndpointPort = 7233;

    /// <summary>The name of the Web UI endpoint.</summary>
    public const string UIEndpointName = "ui";

    /// <summary>The default port for the Web UI endpoint.</summary>
    public const int DefaultUIEndpointPort = 8233;

    /// <summary>The name of the metrics endpoint.</summary>
    public const string MetricsEndpointName = "metrics";

    /// <summary>The default port for the metrics endpoint.</summary>
    public const int DefaultMetricsEndpointPort = 9233;

    /// <summary>The Docker image name for Temporal.</summary>
    public const string TemporalImage = "temporalio/temporal";

    /// <summary>The default Docker image tag.</summary>
    public const string DefaultTag = "latest";

    /// <summary>The default working directory for Temporal CLI execution.</summary>
    public const string DefaultWorkingDirectory = "./";
}
