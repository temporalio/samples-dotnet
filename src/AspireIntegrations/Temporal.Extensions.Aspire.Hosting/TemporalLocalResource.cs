using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalLocalResource(string name)
    : Resource(name), IResourceWithServiceDiscovery
{
    public WorkflowEnvironment? WorkflowEnvironment { get; set; }

    public TemporalResourceOptions Options { get; set; } = new();
}