using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Temporal.Extensions.Aspire.Hosting;
using Xunit;

namespace TemporalioSamples.Tests.AspireIntegrations;

public class TemporalEnvironmentHelperTests
{
    [Fact]
    public void AddEnvironmentVariables_DoesNotInjectDefaultNamespace()
    {
        var environmentVariables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environmentVariables,
            CancellationToken.None);

        var options = new TemporalResourceOptions
        {
            Namespace = "orders",
        };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            context,
            options,
            "localhost:7233",
            "http://localhost:8233");

        Assert.Equal("localhost:7233", environmentVariables["TEMPORAL_ADDRESS"]);
        Assert.Equal("http://localhost:8233", environmentVariables["TEMPORAL_UI_ADDRESS"]);
        Assert.Equal("orders", environmentVariables["TEMPORAL_NAMESPACE"]);
        Assert.False(environmentVariables.ContainsKey("TEMPORAL_DEFAULT_NAMESPACE"));
    }

    [Fact]
    public void AddEnvironmentVariables_UsesIndependentContexts()
    {
        var firstVariables = new Dictionary<string, object>();
        var firstContext = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            firstVariables,
            CancellationToken.None);
        var firstOptions = new TemporalResourceOptions { Namespace = "alpha" };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            firstContext,
            firstOptions,
            "alpha:7233",
            "http://alpha:8233");

        var secondVariables = new Dictionary<string, object>();
        var secondContext = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            secondVariables,
            CancellationToken.None);
        var secondOptions = new TemporalResourceOptions { Namespace = "beta" };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            secondContext,
            secondOptions,
            "beta:7233",
            "http://beta:8233");

        Assert.Equal("alpha", firstVariables["TEMPORAL_NAMESPACE"]);
        Assert.Equal("beta", secondVariables["TEMPORAL_NAMESPACE"]);
        Assert.False(firstVariables.ContainsKey("TEMPORAL_DEFAULT_NAMESPACE"));
        Assert.False(secondVariables.ContainsKey("TEMPORAL_DEFAULT_NAMESPACE"));
    }

    [Fact]
    public void AddEnvironmentVariables_InjectsCodecAuth_WhenSet()
    {
        var environmentVariables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environmentVariables,
            CancellationToken.None);

        var options = new TemporalResourceOptions
        {
            Namespace = "default",
            CodecAuth = "Bearer my-token",
        };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            context, options, "localhost:7233", "http://localhost:8233");

        Assert.Equal("Bearer my-token", environmentVariables["TEMPORAL_CODEC_AUTH"]);
    }

    [Fact]
    public void AddEnvironmentVariables_InjectsCodecEndpoint_WhenSet()
    {
        var environmentVariables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environmentVariables,
            CancellationToken.None);

        var options = new TemporalResourceOptions
        {
            Namespace = "default",
            CodecEndpoint = "http://localhost:8088",
        };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            context, options, "localhost:7233", "http://localhost:8233");

        Assert.Equal("http://localhost:8088", environmentVariables["TEMPORAL_CODEC_ENDPOINT"]);
    }

    [Fact]
    public void AddEnvironmentVariables_OmitsCodecKeys_WhenNotSet()
    {
        // Regression: env vars must not be injected with null/empty values when codec is unused.
        var environmentVariables = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environmentVariables,
            CancellationToken.None);

        var options = new TemporalResourceOptions { Namespace = "default" };

        TemporalEnvironmentHelper.AddEnvironmentVariables(
            context, options, "localhost:7233", "http://localhost:8233");

        Assert.False(environmentVariables.ContainsKey("TEMPORAL_CODEC_AUTH"));
        Assert.False(environmentVariables.ContainsKey("TEMPORAL_CODEC_ENDPOINT"));
    }
}
