using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Health check implementation for Temporal servers.
/// </summary>
public class TemporalHealthCheck(Func<CancellationToken, Task<ITemporalClient?>> clientAccessor) : IHealthCheck
{
    /// <summary>
    /// Checks the health of the Temporal server by calling GetSystemInfoAsync.
    /// </summary>
    /// <param name="context">The health check context providing access to registered services.</param>
    /// <param name="cancellationToken">The cancellation token for this operation.</param>
    /// <returns>A HealthCheckResult indicating whether the server is reachable and healthy.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = await clientAccessor(cancellationToken);
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
