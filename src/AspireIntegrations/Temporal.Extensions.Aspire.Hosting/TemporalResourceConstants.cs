namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalResourceConstants
{
    public const string ServiceEndpointName = "grpc";
    public const int DefaultServiceEndpointPort = 7233;

    public const string UIEndpointName = "ui";
    public const int DefaultUIEndpointPort = 8233;

    public const string MetricsEndpointName = "metrics";
    public const int DefaultMetricsEndpointPort = 9233;

    public const string TemporalImage = "temporalio/temporal";
    public const string DefaultTag = "latest";

    public const string DefaultWorkingDirectory = "./";
}