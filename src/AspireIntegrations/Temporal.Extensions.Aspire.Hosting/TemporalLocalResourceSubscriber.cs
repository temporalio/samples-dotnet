using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Temporalio.Testing;

namespace Temporal.Extensions.Aspire.Hosting;

public class TemporalLocalResourceSubscriber : IDistributedApplicationEventingSubscriber
{
    private readonly Dictionary<string, WorkflowEnvironment> environments = [];

    public Task SubscribeAsync(IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        // Don't start server in publish mode
        if (executionContext.IsPublishMode)
        {
            return Task.CompletedTask;
        }

        // Subscribe to InitializeResourceEvent for each TemporalLocalResource
        eventing.Subscribe<InitializeResourceEvent>(OnInitializeAsync);
        return Task.CompletedTask;
    }

    private async Task OnInitializeAsync(InitializeResourceEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.Resource is not TemporalLocalResource resource)
        {
            return;
        }

        var resourceLoggerService = @event.Services.GetRequiredService<ResourceLoggerService>();

        // Subscribe to ResourceStoppedEvent for this specific resource to handle cleanup
        @event.Eventing.Subscribe<ResourceStoppedEvent>(resource,
            (stopEvent, _) => OnResourceStoppedAsync(stopEvent, resourceLoggerService, resource));

        await StartTemporalTestServerAsync(resource, @event.Eventing,
            @event.Notifications, resourceLoggerService, @event.Services, cancellationToken);
    }

    private async Task StartTemporalTestServerAsync(TemporalLocalResource resource,
        IDistributedApplicationEventing eventing,
        ResourceNotificationService resourceNotificationService,
        ResourceLoggerService resourceLoggerService,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var resourceLogger = resourceLoggerService.GetLogger(resource);

        try
        {
            // Publish starting state
            await resourceNotificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Info),
                StartTimeStamp = DateTime.UtcNow
            });

            resourceLogger.LogInformation("Starting Temporal test server for resource '{ResourceName}'...",
                resource.Name);

            var env = await WorkflowEnvironment.StartLocalAsync(resource.Options);

            // Store the environment for later shutdown (before publishing events)
            environments[resource.Name] = env;

            // Set the environment on the resource so it can be accessed
            resource.WorkflowEnvironment = env;

            var targetHost = env.Client.Connection.Options.TargetHost ?? "unknown";
            var namespaces = string.Join(", ", resource.Options.AdditionalNamespaces);

            resourceLogger.LogInformation(
                "Temporal test server started successfully. Target: {TargetHost}, Namespaces: {Namespaces}",
                targetHost,
                namespaces);

            // Publish running state with properties
            await resourceNotificationService.PublishUpdateAsync(resource, state => state with
            {
                State = KnownResourceStates.Running,
                Properties =
                [
                    new ResourcePropertySnapshot(CustomResourceKnownProperties.Source,
                        "Temporalio.Testing.WorkflowEnvironment"),
                    new ResourcePropertySnapshot("temporal.target-host", targetHost),
                    new ResourcePropertySnapshot("temporal.namespace", resource.Options.Namespace),
                    new ResourcePropertySnapshot("temporal.namespaces", namespaces)
                ]
            });

            // Now publish events (after environment is properly stored and assigned)
            var connectionStringAvailableEvent = new ConnectionStringAvailableEvent(resource, serviceProvider);
            await eventing.PublishAsync(connectionStringAvailableEvent, cancellationToken).ConfigureAwait(false);

            // Publish ResourceReadyEvent to signal that the resource is ready
            await eventing.PublishAsync(new ResourceReadyEvent(resource, serviceProvider), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            resourceLogger.LogError(ex, "Failed to start Temporal test server for resource '{ResourceName}'",
                resource.Name);

            // Publish failed state
            await resourceNotificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error)
            });

            throw;
        }
    }

    private async Task OnResourceStoppedAsync(ResourceStoppedEvent @event,
        ResourceLoggerService resourceLoggerService, TemporalLocalResource resource)
    {
        var resourceName = @event.Resource.Name;
        var resourceLogger = resourceLoggerService.GetLogger(resource);

        // Get environment from resource property first, fallback to tracking dictionary
        var env = resource.WorkflowEnvironment ?? environments.GetValueOrDefault(resourceName);

        if (env != null)
        {
            try
            {
                resourceLogger.LogInformation("Shutting down Temporal test server '{ResourceName}'...", resourceName);
                await env.ShutdownAsync();
                resource.WorkflowEnvironment = null;
                resourceLogger.LogInformation("Temporal test server '{ResourceName}' shut down successfully.",
                    resourceName);
            }
            catch (Exception ex)
            {
                resourceLogger.LogError(ex, "Error shutting down Temporal test server '{ResourceName}'", resourceName);
            }
            finally
            {
                environments.Remove(resourceName);
            }
        }
    }
}