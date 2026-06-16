using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalHealthCheck(Func<ITemporalClient?> clientAccessor) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = clientAccessor();
        if (client is null)
            return HealthCheckResult.Unhealthy("Temporal client not yet initialized");

        try
        {
            await client.WorkflowService.GetSystemInfoAsync(
                new GetSystemInfoRequest(),
                new RpcOptions { CancellationToken = cancellationToken });
            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Unable to reach Temporal server", e);
        }
    }
}
