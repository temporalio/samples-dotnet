using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalContainerBuilderExtensions
{
    public static IResourceBuilder<TemporalContainerResource> AddTemporalDevContainer(
        this IDistributedApplicationBuilder builder,
        string name = "temporal-container",
        Action<TemporalContainerOptions>? configure = null)
    {
        var resource = new TemporalContainerResource(name);
        configure?.Invoke(resource.Options);

        string? endpointAddress = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, _) =>
        {
            if (@event.Resource.TryGetEndpoints(out var endpoints))
            {
                var serviceEndpoint = endpoints.Single(e => e.Name == TemporalResourceConstants.ServiceEndpointName);
                endpointAddress = $"{serviceEndpoint.TargetHost}:{serviceEndpoint.Port}";
            }

            await Task.CompletedTask;
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddTemporalHealthCheck(
                _ => new TemporalClientConnectOptions
                {
                    Namespace = resource.Options.Namespace, TargetHost = endpointAddress
                }, healthCheckKey);

        return builder.AddResource(resource)
            .WithImage(TemporalResourceConstants.TemporalImage,
                resource.Options.ImageTag ?? TemporalResourceConstants.DefaultTag)
            .WithImageRegistry("docker.io")
            .WithArgs(BuildContainerArgs(resource.Options))
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
                ctx.EnvironmentVariables["TEMPORAL_ADDRESS"] =
                    source.Resource.PrimaryEndpoint.Property(EndpointProperty.HostAndPort);

                ctx.EnvironmentVariables["TEMPORAL_UI_ADDRESS"] =
                    source.GetEndpoint(TemporalResourceConstants.UIEndpointName);

                if (!string.IsNullOrEmpty(source.Resource.Options.CodecAuth))
                {
                    ctx.EnvironmentVariables["TEMPORAL_CODEC_AUTH"] = source.Resource.Options.CodecAuth;
                }

                if (!string.IsNullOrEmpty(source.Resource.Options.CodecEndpoint))
                {
                    ctx.EnvironmentVariables["TEMPORAL_CODEC_ENDPOINT"] = source.Resource.Options.CodecEndpoint;
                }
            });
    }

    private static string[] BuildContainerArgs(TemporalResourceOptions options)
    {
        var args = new List<string> { "server", "start-dev" };

        args.AddRange(["--ip", "0.0.0.0"]);
        args.AddRange(["--port", $"{TemporalResourceConstants.DefaultServiceEndpointPort}"]);

        if (options.IsHeadless)
            args.Add("--headless");

        args.AddRange(["--log-level", options.DevServerOptions.LogLevel]);

        args.AddRange(["--log-format", options.DevServerOptions.LogFormat]);

        foreach (var ns in options.AdditionalNamespaces)
            args.AddRange(["--namespace", ns]);

        // Add search attributes from inherited property
        if (options.SearchAttributes != null)
        {
            foreach (var sa in options.SearchAttributes)
            {
                args.AddRange(["--search-attribute", $"{sa.Name}={sa.ValueType}"]);
            }
        }

        foreach (var dv in options.DynamicConfigValues)
            args.AddRange(["--dynamic-config-value", dv]);

        if (!string.IsNullOrEmpty(options.CodecAuth))
            args.AddRange(["--codec-auth", options.CodecAuth]);

        if (!string.IsNullOrEmpty(options.CodecEndpoint))
            args.AddRange(["--codec-endpoint", options.CodecEndpoint]);

        if (!string.IsNullOrEmpty(options.ApiKey))
            args.AddRange(["--api-key", options.ApiKey]);

        return args.ToArray();
    }
}