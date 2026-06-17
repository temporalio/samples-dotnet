using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Represents a Temporal server running locally via <see cref="WorkflowEnvironment"/>.
/// </summary>
public class TemporalLocalResource(string name)
    : Resource(name), IResourceWithServiceDiscovery
{
    /// <summary>Gets or sets the underlying workflow environment instance.</summary>
    public WorkflowEnvironment? WorkflowEnvironment { get; set; }

    /// <summary>Gets or sets the resource configuration options.</summary>
    public TemporalResourceOptions Options { get; set; } = new();
}
