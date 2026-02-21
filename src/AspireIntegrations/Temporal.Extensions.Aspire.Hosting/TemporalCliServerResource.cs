namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalCliServerResource(string name, string workingDirectory = "./")
    : ExecutableResource(name, "temporal", workingDirectory), IResourceWithConnectionString,
        IResourceWithServiceDiscovery
{
    private EndpointReference? primaryEndpoint;

    public EndpointReference PrimaryEndpoint =>
        primaryEndpoint ??= new(this, TemporalResourceConstants.ServiceEndpointName);

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    public TemporalResourceOptions Options { get; set; } = new();
}