using Microsoft.Extensions.DependencyInjection;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalCliServerResourceExtensions
{
    public static IResourceBuilder<TemporalCliServerResource> AddTemporalCliServer(
        this IDistributedApplicationBuilder builder,
        string name = "temporal-cli-server",
        Action<TemporalResourceOptions>? configure = null)
    {
        return builder.AddTemporalCliServer(name, configure, isTemporalCliAvailable: null);
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalCliServerResource> source)
        where TDestination : IResourceWithEnvironment
    {
        return builder
            .WithReference((IResourceBuilder<IResourceWithServiceDiscovery>)source)
            .WithEnvironment(ctx =>
            {
                TemporalEnvironmentHelper.AddEnvironmentVariables(
                    ctx,
                    source.Resource.Options,
                    source.Resource.ConnectionStringExpression,
                    source.GetEndpoint(TemporalResourceConstants.UIEndpointName));
            });
    }

    // Internal overload that accepts an injectable CLI availability check.
    // Used by tests to simulate an absent 'temporal' binary without modifying PATH.
    internal static IResourceBuilder<TemporalCliServerResource> AddTemporalCliServer(
        this IDistributedApplicationBuilder builder,
        string name,
        Action<TemporalResourceOptions>? configure,
        Func<bool>? isTemporalCliAvailable)
    {
        TemporalCliLocator.EnsureAvailable(isTemporalCliAvailable);
        var resource = new TemporalCliServerResource(name);
        configure?.Invoke(resource.Options);

        var clientAccessor = TemporalHealthCheckHelper.RegisterCachedClientAccessor(
            builder, resource, resource.Options.Namespace);

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddTemporalHealthCheck(clientAccessor, healthCheckKey);

        return builder.AddResource(resource)
            .WithArgs(TemporalArgsBuilder.BuildArgs(resource.Options))
            .ExcludeFromManifest()
            .WithEndpoint(
                targetPort: resource.Options.Port,
                port: resource.Options.Port,
                isProxied: false,
                name: TemporalResourceConstants.ServiceEndpointName)
            .WithHttpEndpoint(
                targetPort: resource.Options.UIPort,
                port: resource.Options.UIPort,
                isProxied: false,
                name: TemporalResourceConstants.UIEndpointName)
            .WithHttpEndpoint(
                targetPort: resource.Options.MetricsPort,
                port: resource.Options.MetricsPort,
                isProxied: false,
                name: TemporalResourceConstants.MetricsEndpointName)
            .WithHealthCheck(healthCheckKey)
            .WithUrlForEndpoint(TemporalResourceConstants.UIEndpointName, url =>
            {
                url.DisplayText = "Dashboard";
            });
    }
}
