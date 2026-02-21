using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

public static class TemporalLocalResourceExtensions
{
    public static IResourceBuilder<TemporalLocalResource> AddTemporalLocalTestServer(
        this IDistributedApplicationBuilder builder,
        string name = "temporal-local",
        Action<TemporalResourceOptions>? configure = null)
    {
        builder.Services.TryAddEventingSubscriber<TemporalLocalResourceSubscriber>();

        var resource = new TemporalLocalResource(name);

        configure?.Invoke(resource.Options);

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddTemporalHealthCheck(_ => new TemporalClientConnectOptions
            {
                Namespace = resource.Options.Namespace,
                TargetHost = resource.WorkflowEnvironment?.Client.Connection.Options.TargetHost
            }, healthCheckKey);

        var resourceBuilder = builder.AddResource(resource)
            .ExcludeFromManifest()
            .WithEndpoint(
                targetPort: resource.Options.Port,
                port: resource.Options.Port,
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
            .WithUrlForEndpoint(TemporalResourceConstants.UIEndpointName, url => { url.DisplayText = "Dashboard"; })
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "temporal-local",
                CreationTimeStamp = DateTime.UtcNow,
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new(CustomResourceKnownProperties.Source, "Temporalio.Testing.WorkflowEnvironment")
                ]
            });

        resourceBuilder.WithCommand(
            name: KnownResourceCommands.StopCommand,
            displayName: "Stop",
            executeCommand: async context =>
            {
                var notifications = context.ServiceProvider
                    .GetRequiredService<ResourceNotificationService>();
                var resourceLogger = context.ServiceProvider
                    .GetRequiredService<ResourceLoggerService>()
                    .GetLogger(resource);
                var eventing = context.ServiceProvider
                    .GetRequiredService<IDistributedApplicationEventing>();

                await notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = KnownResourceStates.Stopping
                });

                try
                {
                    if (resource.WorkflowEnvironment != null)
                    {
                        resourceLogger.LogInformation("Shutting down Temporal test server '{ResourceName}'...", resource.Name);
                        await resource.WorkflowEnvironment.ShutdownAsync();
                        resource.WorkflowEnvironment = null;
                        resourceLogger.LogInformation("Temporal test server '{ResourceName}' shut down successfully.", resource.Name);
                    }

                    // Publish ResourceStoppedEvent to trigger subscriber cleanup and keep _environments dictionary in sync
                    var resourceEvent = new ResourceEvent(resource, resource.Name, new CustomResourceSnapshot
                    {
                        ResourceType = "temporal-local",
                        CreationTimeStamp = DateTime.UtcNow,
                        State = KnownResourceStates.Exited,
                        Properties = []
                    });
                    var stoppedEvent = new ResourceStoppedEvent(resource, context.ServiceProvider, resourceEvent);
                    await eventing.PublishAsync(stoppedEvent, context.CancellationToken);

                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        State = KnownResourceStates.Exited
                    });

                    return CommandResults.Success();
                }
                catch (Exception ex)
                {
                    resourceLogger.LogError(ex, "Error shutting down Temporal test server '{ResourceName}'", resource.Name);
                    return CommandResults.Failure(ex.Message);
                }
            },
            commandOptions: new CommandOptions
            {
                IconName = "Stop",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = context =>
                {
                    var state = context.ResourceSnapshot.State?.Text;
                    if (IsStarting(state) || HasNoState(state))
                    {
                        return ResourceCommandState.Disabled;
                    }
                    else if (IsRunning(state))
                    {
                        return ResourceCommandState.Enabled;
                    }
                    else
                    {
                        return ResourceCommandState.Hidden;
                    }
                }
            });

        resourceBuilder.WithCommand(
            name: KnownResourceCommands.StartCommand,
            displayName: "Start",
            executeCommand: async context =>
            {
                var notifications = context.ServiceProvider
                    .GetRequiredService<ResourceNotificationService>();
                var resourceLogger = context.ServiceProvider
                    .GetRequiredService<ResourceLoggerService>()
                    .GetLogger(resource);
                var eventing = context.ServiceProvider
                    .GetRequiredService<IDistributedApplicationEventing>();

                await notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = KnownResourceStates.Starting
                });

                try
                {
                    resourceLogger.LogInformation("Starting Temporal test server for resource '{ResourceName}'...", resource.Name);
                    var env = await WorkflowEnvironment.StartLocalAsync(resource.Options);
                    resource.WorkflowEnvironment = env;

                    var targetHost = env.Client.Connection.Options.TargetHost ?? "unknown";

                    resourceLogger.LogInformation(
                        "Temporal test server started successfully. Target: {TargetHost}, Namespace: {Namespace}",
                        targetHost,
                        resource.Options.Namespace);

                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        State = KnownResourceStates.Running,
                        Properties =
                        [
                            new ResourcePropertySnapshot(CustomResourceKnownProperties.Source,
                                "Temporalio.Testing.WorkflowEnvironment"),
                            new ResourcePropertySnapshot("temporal.target-host", targetHost),
                            new ResourcePropertySnapshot("temporal.namespace", resource.Options.Namespace)
                        ]
                    });

                    // Publish ResourceReadyEvent to signal that the resource is ready
                    await eventing.PublishAsync(new ResourceReadyEvent(resource, context.ServiceProvider), context.CancellationToken);

                    return CommandResults.Success();
                }
                catch (Exception ex)
                {
                    resourceLogger.LogError(ex, "Failed to start Temporal test server for resource '{ResourceName}'", resource.Name);
                    await notifications.PublishUpdateAsync(resource, s => s with
                    {
                        State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error)
                    });
                    return CommandResults.Failure(ex.Message);
                }
            },
            commandOptions: new CommandOptions
            {
                IconName = "Play",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = context =>
                {
                    var state = context.ResourceSnapshot.State?.Text;
                    if (IsStarting(state) || IsRuntimeUnhealthy(state) || HasNoState(state))
                    {
                        return ResourceCommandState.Disabled;
                    }

                    if (IsStopped(state) || IsWaiting(state))
                    {
                        return ResourceCommandState.Enabled;
                    }

                    return ResourceCommandState.Hidden;
                }
            });

        return resourceBuilder;

        static bool IsStopped(string? state) => KnownResourceStates.TerminalStates.Contains(state) ||
                                                state == KnownResourceStates.NotStarted || state == "Unknown";

        static bool IsRunning(string? state) => state == KnownResourceStates.Running;
        static bool IsStarting(string? state) => state == KnownResourceStates.Starting;
        static bool IsWaiting(string? state) => state == KnownResourceStates.Waiting;
        static bool IsRuntimeUnhealthy(string? state) => state == KnownResourceStates.RuntimeUnhealthy;
        static bool HasNoState(string? state) => string.IsNullOrEmpty(state);
    }

    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder, IResourceBuilder<TemporalLocalResource> source)
        where TDestination : IResourceWithEnvironment
    {
        return builder
            .WithEnvironment(ctx =>
            {
                ctx.EnvironmentVariables["TEMPORAL_ADDRESS"] =
                    $"localhost:{source.Resource.Options.Port}";

                ctx.EnvironmentVariables["TEMPORAL_UI_ADDRESS"] =
                    $"http://localhost:{source.Resource.Options.UIPort}";

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
}