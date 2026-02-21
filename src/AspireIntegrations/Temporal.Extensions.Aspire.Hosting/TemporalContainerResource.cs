namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString, IResourceWithServiceDiscovery
{
    private EndpointReference? primaryEndpoint;

    public EndpointReference PrimaryEndpoint => primaryEndpoint ??= new(this, TemporalResourceConstants.ServiceEndpointName);

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.HostAndPort)}");

    public TemporalContainerOptions Options { get; set; } = new();
}