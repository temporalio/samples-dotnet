using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Configuration options for Temporal resources (local, container, and CLI-based).
/// Extends <see cref="WorkflowEnvironmentStartLocalOptions"/> with Aspire-specific settings.
/// </summary>
public class TemporalResourceOptions : WorkflowEnvironmentStartLocalOptions
{
    private List<string> extraNamespaces = [];

    public TemporalResourceOptions()
    {
        // Set defaults that differ from base class
        UIPort = TemporalResourceConstants.DefaultUIEndpointPort;
        UI = true;
        TargetHost = $"0.0.0.0:{TemporalResourceConstants.DefaultServiceEndpointPort}";
    }

    /// <summary>
    /// Gets or sets additional namespaces beyond the primary namespace.
    /// Always includes the primary <see cref="WorkflowEnvironmentStartLocalOptions.Namespace"/> in the returned list.
    /// </summary>
    public new List<string> AdditionalNamespaces
    {
        get
        {
            var result = new List<string> { Namespace };
            foreach (var ns in extraNamespaces)
            {
                if (ns != Namespace && !result.Contains(ns))
                    result.Add(ns);
            }
            return result;
        }

        set
        {
            extraNamespaces = value == null
                ? []
                : value.Where(ns => !string.IsNullOrEmpty(ns) && ns != Namespace).Distinct().ToList();
        }
    }

    /// <summary>
    /// Gets the gRPC port, parsed from <see cref="WorkflowEnvironmentStartLocalOptions.TargetHost"/>.
    /// </summary>
    public int Port
    {
        get
        {
            if (string.IsNullOrEmpty(TargetHost))
                throw new InvalidOperationException("TargetHost must be set before accessing Port.");

            var parts = TargetHost.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                return port;

            throw new InvalidOperationException($"TargetHost '{TargetHost}' is not in the expected 'ip:port' format.");
        }
    }

    /// <summary>
    /// Gets the IP address to bind to, parsed from TargetHost.
    /// Maps to --ip CLI argument and DevServerOptions.Ip concept.
    /// </summary>
    public string Ip
    {
        get
        {
            if (string.IsNullOrEmpty(TargetHost))
                throw new InvalidOperationException("TargetHost must be set before accessing Ip.");

            var parts = TargetHost.Split(':');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                return parts[0];

            return "0.0.0.0";
        }
    }

    /// <summary>Gets or sets the metrics endpoint port. Default is 9233.</summary>
    public int MetricsPort { get; set; } = TemporalResourceConstants.DefaultMetricsEndpointPort;

    /// <summary>Gets a value indicating whether the UI is disabled.</summary>
    public bool IsHeadless => !UI;

    /// <summary>Gets or sets dynamic configuration values for the Temporal server.</summary>
    public List<string> DynamicConfigValues { get; set; } = [];

    /// <summary>Gets or sets the codec authentication token for encrypted payloads.</summary>
    public string? CodecAuth { get; set; }

    /// <summary>Gets or sets the codec server endpoint for encrypted payloads.</summary>
    public string? CodecEndpoint { get; set; }
}
