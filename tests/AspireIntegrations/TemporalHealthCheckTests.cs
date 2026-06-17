using Microsoft.Extensions.Diagnostics.HealthChecks;
using Temporal.Extensions.Aspire.Hosting;
using Temporalio.Client;
using Xunit;

namespace TemporalioSamples.Tests.AspireIntegrations;

public class TemporalHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenClientAccessorReturnsNull()
    {
        // Regression: before the lazy-reconnect refactor, a null client meant the resource stayed
        // permanently Unhealthy with no path to recovery. The fix ensures the description is
        // "not yet initialized" (a transient state) rather than a hard failure.
        var healthCheck = new TemporalHealthCheck(_ => Task.FromResult<ITemporalClient?>(null));

        var result = await healthCheck.CheckHealthAsync(MakeContext(healthCheck), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not yet initialized", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenAccessorAlwaysReturnsNull_StatusIsTransient()
    {
        // A second probe with a still-null client also returns Unhealthy, confirming the accessor
        // is called on every probe (lazy, not just once).
        var callCount = 0;
        var healthCheck = new TemporalHealthCheck(_ =>
        {
            callCount++;
            return Task.FromResult<ITemporalClient?>(null);
        });

        await healthCheck.CheckHealthAsync(MakeContext(healthCheck), CancellationToken.None);
        await healthCheck.CheckHealthAsync(MakeContext(healthCheck), CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task CheckHealthAsync_PropagatesCancellation_WhenTokenCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var healthCheck = new TemporalHealthCheck(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<ITemporalClient?>(null);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => healthCheck.CheckHealthAsync(MakeContext(healthCheck), cts.Token));
    }

    private static HealthCheckContext MakeContext(IHealthCheck instance) =>
        new() { Registration = new HealthCheckRegistration("temporal", instance, HealthStatus.Unhealthy, null) };
}
