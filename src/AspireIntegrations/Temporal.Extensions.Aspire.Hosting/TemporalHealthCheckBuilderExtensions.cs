using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Extension methods for adding Temporal health checks to Aspire applications.
/// </summary>
public static class TemporalHealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a Temporal health check to the health checks builder.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="clientAccessor">A function that provides access to a Temporal client.</param>
    /// <param name="name">The health check name. Default is "temporal".</param>
    /// <param name="tags">Optional tags to associate with the health check.</param>
    /// <param name="timeout">Optional timeout for the health check.</param>
    /// <returns>The updated health checks builder.</returns>
    public static IHealthChecksBuilder AddTemporalHealthCheck(
        this IHealthChecksBuilder builder,
        Func<CancellationToken, Task<ITemporalClient?>> clientAccessor,
        string name = "temporal",
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            _ => new TemporalHealthCheck(clientAccessor),
            HealthStatus.Unhealthy,
            tags,
            timeout));
    }
}
