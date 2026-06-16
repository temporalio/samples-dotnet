using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

internal static class TemporalHealthCheckHelper
{
    /// <summary>
    /// Subscribes to <see cref="ConnectionStringAvailableEvent"/> for a CLI or container resource,
    /// creates a <see cref="ITemporalClient"/> once the endpoint is available, and returns an accessor
    /// that the health check can call on every probe.
    /// The cached client is replaced on each subsequent event so restarts are covered.
    /// </summary>
    internal static Func<ITemporalClient?> RegisterCachedClientAccessor(
        IDistributedApplicationBuilder builder,
        IResource resource,
        string @namespace)
    {
        ITemporalClient? cachedClient = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, _) =>
        {
            if (@event.Resource.TryGetEndpoints(out var endpoints))
            {
                var serviceEndpoint = endpoints.Single(e => e.Name == TemporalResourceConstants.ServiceEndpointName);
                var hostPort = $"{serviceEndpoint.TargetHost}:{serviceEndpoint.Port}";
                cachedClient = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions
                {
                    Namespace = @namespace,
                    TargetHost = hostPort
                });
            }
        });

        return () => cachedClient;
    }
}
