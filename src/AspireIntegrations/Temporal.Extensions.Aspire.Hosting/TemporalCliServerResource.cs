namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Represents a Temporal server running via the CLI executable.
/// </summary>
public class TemporalCliServerResource(string name, string workingDirectory = "./")
    : ExecutableResource(name, "temporal", workingDirectory), IResourceWithConnectionString,
        IResourceWithServiceDiscovery
{
    private EndpointReference? primaryEndpoint;

    /// <summary>Gets the primary gRPC service endpoint.</summary>
    public EndpointReference PrimaryEndpoint =>
        primaryEndpoint ??= new(this, TemporalResourceConstants.ServiceEndpointName);

    /// <summary>Gets the connection string expression for dependent services.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.HostAndPort)}");

    /// <summary>Gets or sets the resource configuration options.</summary>
    public TemporalResourceOptions Options { get; set; } = new();
}
