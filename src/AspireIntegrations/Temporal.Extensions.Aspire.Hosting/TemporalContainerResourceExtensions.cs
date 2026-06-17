using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalContainerResourceExtensions
{
    public static IResourceBuilder<TemporalContainerResource> AddTemporalDevContainer(
        this IDistributedApplicationBuilder builder,
        string name = "temporal-container",
        Action<TemporalContainerOptions>? configure = null)
    {
        var resource = new TemporalContainerResource(name);
        configure?.Invoke(resource.Options);

        var clientAccessor = TemporalHealthCheckHelper.RegisterCachedClientAccessor(
            builder, resource, resource.Options.Namespace);

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddTemporalHealthCheck(clientAccessor, healthCheckKey);

        return builder.AddResource(resource)
            .WithImage(TemporalResourceConstants.TemporalImage,
                resource.Options.ImageTag ?? TemporalResourceConstants.DefaultTag)
            .WithImageRegistry("docker.io")
            .WithArgs(TemporalArgsBuilder.BuildArgs(resource.Options, fixedIpAndPort: true))
            .ExcludeFromManifest()
            .WithEndpoint(
                targetPort: TemporalResourceConstants.DefaultServiceEndpointPort,
                port: resource.Options.Port,
                name: TemporalResourceConstants.ServiceEndpointName)
            .WithHttpEndpoint(
                targetPort: TemporalResourceConstants.DefaultUIEndpointPort,
                port: resource.Options.UIPort,
                name: TemporalResourceConstants.UIEndpointName)
            .WithHttpEndpoint(
                targetPort: resource.Options.MetricsPort,
                port: resource.Options.MetricsPort,
                name: TemporalResourceConstants.MetricsEndpointName)
            .WithHealthCheck(healthCheckKey)
            .WithUrlForEndpoint(TemporalResourceConstants.UIEndpointName, url =>
            {
                url.DisplayText = "Dashboard";
            });
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalContainerResource> source)
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
