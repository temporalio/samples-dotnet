using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddTemporalHealthCheck(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, TemporalClientConnectOptions> clientConnectOptionsFactory,
        string name = "temporal",
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new TemporalHealthCheck(clientConnectOptionsFactory(sp)),
            HealthStatus.Unhealthy,
            tags,
            timeout));
    }
}