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
    internal static Func<CancellationToken, Task<ITemporalClient?>> RegisterCachedClientAccessor(
        IDistributedApplicationBuilder builder,
        IResource resource,
        string @namespace)
    {
        ITemporalClient? cachedClient = null;
        string? hostPort = null;

        async Task<ITemporalClient?> EnsureClientConnectedAsync(CancellationToken cancellationToken)
        {
            if (cachedClient is not null)
                return cachedClient;

            if (string.IsNullOrEmpty(hostPort))
                return null;

            try
            {
                cachedClient = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions
                {
                    Namespace = @namespace,
                    TargetHost = hostPort
                });
                return cachedClient;
            }
            catch (InvalidOperationException)
            {
                cachedClient = null;
                return null;
            }
        }

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, _) =>
        {
            try
            {
                if (!@event.Resource.TryGetEndpoints(out var endpoints))
                    return;

                var serviceEndpoint = endpoints.Single(e => e.Name == TemporalResourceConstants.ServiceEndpointName);
                hostPort = $"{serviceEndpoint.TargetHost}:{serviceEndpoint.Port}";
                cachedClient = null;

                // The endpoint can be published before Temporal is accepting connections.
                // Retry a few times but never throw from this callback.
                const int maxAttempts = 30;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var client = await EnsureClientConnectedAsync(CancellationToken.None);
                    if (client is not null)
                        break;

                    if (attempt < maxAttempts)
                        await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
                }
            }
            catch
            {
                // Resource startup must not fail because client warm-up failed.
                cachedClient = null;
            }
        });

        return EnsureClientConnectedAsync;
    }
}
