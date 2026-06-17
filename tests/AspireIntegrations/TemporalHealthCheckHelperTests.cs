using Aspire.Hosting;
using Temporal.Extensions.Aspire.Hosting;
using Xunit;

namespace TemporalioSamples.Tests.AspireIntegrations;

public class TemporalHealthCheckHelperTests
{
    [Fact]
    public async Task AccessorReturnsNull_BeforeConnectionStringEventFires()
    {
        // Regression: the old design attempted to connect inside the ConnectionStringAvailableEvent
        // callback and threw when the port wasn't bound yet, causing FailedToStart. The new design
        // has the accessor return null (Unhealthy "not yet initialized") until the first successful
        // connect, either during warm-up retries or on a later health probe.
        //
        // This test ensures that calling the accessor immediately after registration — before any
        // Aspire event has fired — returns null rather than throwing.
        var appBuilder = DistributedApplication.CreateBuilder([]);
        var resource = new TemporalCliServerResource("test-temporal");

        var accessor = TemporalHealthCheckHelper.RegisterCachedClientAccessor(
            appBuilder, resource, "default");

        var client = await accessor(CancellationToken.None);

        Assert.Null(client);
    }

    [Fact]
    public async Task AccessorIsIdempotent_WhenCalledMultipleTimes_BeforeEventFires()
    {
        // The accessor must be safe to call repeatedly (health checks probe on every interval).
        // It must never throw when no connection string has been published yet.
        var appBuilder = DistributedApplication.CreateBuilder([]);
        var resource = new TemporalCliServerResource("test-temporal");

        var accessor = TemporalHealthCheckHelper.RegisterCachedClientAccessor(
            appBuilder, resource, "default");

        for (var i = 0; i < 3; i++)
        {
            var client = await accessor(CancellationToken.None);
            Assert.Null(client);
        }
    }
}
