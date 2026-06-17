namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Configuration options specific to Temporal Docker container deployments.
/// </summary>
public class TemporalContainerOptions : TemporalResourceOptions
{
    /// <summary>Gets or sets the Docker image tag. Default is "latest".</summary>
    public string? ImageTag { get; set; } = TemporalResourceConstants.DefaultTag;
}
