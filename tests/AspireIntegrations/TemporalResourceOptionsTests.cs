using Temporal.Extensions.Aspire.Hosting;
using Xunit;

namespace TemporalioSamples.Tests.AspireIntegrations;

public class TemporalResourceOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedPortValues()
    {
        var options = new TemporalResourceOptions();

        Assert.Equal(TemporalResourceConstants.DefaultServiceEndpointPort, options.Port);
        Assert.Equal(TemporalResourceConstants.DefaultUIEndpointPort, options.UIPort);
        Assert.Equal(TemporalResourceConstants.DefaultMetricsEndpointPort, options.MetricsPort);
    }

    [Fact]
    public void DefaultOptions_HaveUIEnabled()
    {
        var options = new TemporalResourceOptions();

        Assert.True(options.UI);
        Assert.False(options.IsHeadless);
    }

    [Fact]
    public void Port_ParsedCorrectlyFromTargetHost()
    {
        var options = new TemporalResourceOptions { TargetHost = "0.0.0.0:7240" };

        Assert.Equal(7240, options.Port);
    }

    [Fact]
    public void Ip_ParsedCorrectlyFromTargetHost()
    {
        var options = new TemporalResourceOptions { TargetHost = "127.0.0.1:7233" };

        Assert.Equal("127.0.0.1", options.Ip);
    }

    [Fact]
    public void Port_ThrowsInvalidOperationException_WhenTargetHostIsEmpty()
    {
        var options = new TemporalResourceOptions { TargetHost = string.Empty };

        Assert.Throws<InvalidOperationException>(() => _ = options.Port);
    }

    [Fact]
    public void Ip_ThrowsInvalidOperationException_WhenTargetHostIsEmpty()
    {
        var options = new TemporalResourceOptions { TargetHost = string.Empty };

        Assert.Throws<InvalidOperationException>(() => _ = options.Ip);
    }

    [Fact]
    public void Port_ThrowsInvalidOperationException_WhenTargetHostLacksPort()
    {
        // Regression: TargetHost with no colon-separated port must not silently return 0.
        var options = new TemporalResourceOptions { TargetHost = "localhost" };

        Assert.Throws<InvalidOperationException>(() => _ = options.Port);
    }

    [Fact]
    public void AdditionalNamespaces_AlwaysIncludesPrimaryNamespace()
    {
        var options = new TemporalResourceOptions { Namespace = "orders" };
        options.AdditionalNamespaces = new List<string> { "shipping", "payments" };

        Assert.Contains("orders", options.AdditionalNamespaces);
        Assert.Contains("shipping", options.AdditionalNamespaces);
        Assert.Contains("payments", options.AdditionalNamespaces);
    }

    [Fact]
    public void AdditionalNamespaces_DeduplicatesPrimaryNamespace()
    {
        // Setting the primary namespace in the extra list must not create a duplicate.
        var options = new TemporalResourceOptions { Namespace = "orders" };
        options.AdditionalNamespaces = new List<string> { "orders", "shipping" };

        Assert.Single(options.AdditionalNamespaces, ns => ns == "orders");
    }
}
