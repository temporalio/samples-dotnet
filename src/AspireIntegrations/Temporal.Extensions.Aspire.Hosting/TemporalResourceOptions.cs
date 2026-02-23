using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalResourceOptions : WorkflowEnvironmentStartLocalOptions
{
    private List<string> additionalNamespaces;

    public TemporalResourceOptions()
    {
        // Set defaults that differ from base class
        UIPort = TemporalResourceConstants.DefaultUIEndpointPort;
        UI = true;
        TargetHost = $"0.0.0.0:{TemporalResourceConstants.DefaultServiceEndpointPort}";

        // Initialize AdditionalNamespaces with the primary namespace
        additionalNamespaces = [Namespace];
    }

    public new List<string> AdditionalNamespaces
    {
        get => additionalNamespaces.Count > 0 ? additionalNamespaces : [Namespace];
        set => additionalNamespaces = value ?? [];
    }

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

    public int MetricsPort { get; set; } = TemporalResourceConstants.DefaultMetricsEndpointPort;

    public bool IsHeadless => !UI;

    public List<string> DynamicConfigValues { get; } = [];

    public string? CodecAuth { get; set; }

    public string? CodecEndpoint { get; set; }
}