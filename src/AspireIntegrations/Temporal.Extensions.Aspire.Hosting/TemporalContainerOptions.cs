namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalContainerOptions : TemporalResourceOptions
{
    public string? ImageTag { get; set; } = TemporalResourceConstants.DefaultTag;
}