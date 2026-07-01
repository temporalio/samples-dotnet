namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Represents a Temporal server running as a Docker container.
/// </summary>
public class TemporalContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString, IResourceWithServiceDiscovery
{
    private EndpointReference? primaryEndpoint;

    /// <summary>Gets the primary gRPC service endpoint.</summary>
    public EndpointReference PrimaryEndpoint => primaryEndpoint ??= new(this, TemporalResourceConstants.ServiceEndpointName);

    /// <summary>Gets the connection string expression for dependent services.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.HostAndPort)}");

    /// <summary>Gets or sets the container configuration options.</summary>
    public TemporalContainerOptions Options { get; set; } = new();
}
