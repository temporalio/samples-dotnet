using Microsoft.Extensions.DependencyInjection;

namespace Temporal.Extensions.Aspire.Hosting;

/// <summary>
/// Extension methods for registering Temporal container resources in Aspire.
/// </summary>
public static class TemporalContainerResourceExtensions
{
    /// <summary>
    /// Adds a Temporal Docker container resource to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name. Default is "temporal-container".</param>
    /// <param name="configure">Optional action to configure the container options.</param>
    /// <returns>A builder for the Temporal container resource.</returns>
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
                isProxied: false,
                name: TemporalResourceConstants.ServiceEndpointName)
            .WithHttpEndpoint(
                targetPort: TemporalResourceConstants.DefaultUIEndpointPort,
                port: resource.Options.UIPort,
                isProxied: false,
                name: TemporalResourceConstants.UIEndpointName)
            .WithHttpEndpoint(
                targetPort: TemporalResourceConstants.DefaultMetricsEndpointPort,
                port: resource.Options.MetricsPort,
                isProxied: false,
                name: TemporalResourceConstants.MetricsEndpointName)
            .WithHealthCheck(healthCheckKey)
            .WithUrlForEndpoint(TemporalResourceConstants.UIEndpointName, url =>
            {
                url.DisplayText = "Dashboard";
            });
    }

    /// <summary>
    /// Adds a reference from a dependent service to a Temporal container resource,
    /// automatically injecting connection environment variables.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination resource.</typeparam>
    /// <param name="builder">The resource builder for the dependent service.</param>
    /// <param name="source">The Temporal container resource builder.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalContainerResource> source)
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
}
