using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalHealthCheck(TemporalClientConnectOptions clientConnectOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await TemporalClient.ConnectAsync(clientConnectOptions);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Unable to connect to Temporal server", e);
        }
    }
}