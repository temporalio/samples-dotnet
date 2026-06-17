using Microsoft.Extensions.DependencyInjection;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalCliServerResourceExtensions
{
    public static IResourceBuilder<TemporalCliServerResource> AddTemporalCliServer(
        this IDistributedApplicationBuilder builder,
        string name = "temporal-cli-server",
        Action<TemporalResourceOptions>? configure = null)
    {
        TemporalCliLocator.EnsureAvailable();
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
                isProxied: false,
                name: TemporalResourceConstants.ServiceEndpointName)
            .WithHttpEndpoint(
                targetPort: resource.Options.UIPort,
                isProxied: false,
                name: TemporalResourceConstants.UIEndpointName)
            .WithHttpEndpoint(
                targetPort: resource.Options.MetricsPort,
                isProxied: false,
                name: TemporalResourceConstants.MetricsEndpointName)
            .WithHealthCheck(healthCheckKey)
            .WithUrlForEndpoint(TemporalResourceConstants.UIEndpointName, url =>
            {
                url.DisplayText = "Dashboard";
            });
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalCliServerResource> source)
        where TDestination : IResourceWithEnvironment
    {
        return builder
            .WithReference(source as IResourceBuilder<IResourceWithServiceDiscovery>)
            .WithEnvironment(ctx =>
            {
                TemporalEnvironmentHelper.AddEnvironmentVariables(
                    ctx,
                    source.Resource.Options,
                    source.Resource.ConnectionStringExpression,
                    source.GetEndpoint(TemporalResourceConstants.UIEndpointName));
            });
    }
}
