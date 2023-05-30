namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using TemporalioSamples.ActivityDependencyInjection;

/// <summary>
/// Temporal activity extensions for <see cref="IServiceCollection" />.
/// </summary>
public static class TemporalActivityServiceCollectionExtensions
{
    /// <summary>
    /// Add singleton via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection)" />
    /// then configure all activities via
    /// <see cref="ConfigureTemporalActivities{T}(IServiceCollection, string?)" />.
    /// </summary>
    /// <typeparam name="T">Activity class type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection AddTemporalActivitySingleton<T>(
        this IServiceCollection services,
        string? specificTaskQueue = null) =>
        services.AddTemporalActivitySingleton(typeof(T), specificTaskQueue);

    /// <summary>
    /// Add singleton via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton(IServiceCollection, Type)" />
    /// then configure all activities via
    /// <see cref="ConfigureTemporalActivities(IServiceCollection, Type, string?)" />.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="type">Activity class type.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection AddTemporalActivitySingleton(
        this IServiceCollection services,
        Type type,
        string? specificTaskQueue = null)
    {
        services.TryAddSingleton(type);
        return services.ConfigureTemporalActivities(type, specificTaskQueue);
    }

    /// <summary>
    /// Add transient via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddTransient{TService}(IServiceCollection)" />
    /// then configure all activities via
    /// <see cref="ConfigureTemporalActivities{T}(IServiceCollection, string?)" />.
    /// </summary>
    /// <typeparam name="T">Activity class type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection AddTemporalActivityTransient<T>(
        this IServiceCollection services,
        string? specificTaskQueue = null) =>
        services.AddTemporalActivityTransient(typeof(T), specificTaskQueue);

    /// <summary>
    /// Add transient via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddTransient(IServiceCollection, Type)" />
    /// then configure all activities via
    /// <see cref="ConfigureTemporalActivities(IServiceCollection, Type, string?)" />.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="type">Activity class type.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection AddTemporalActivityTransient(
        this IServiceCollection services,
        Type type,
        string? specificTaskQueue = null)
    {
        services.TryAddTransient(type);
        return services.ConfigureTemporalActivities(type, specificTaskQueue);
    }

    /// <summary>
    /// Configure all activity methods for the given type on <see cref="ActivityOptions" />.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <typeparam name="T">Activity class type.</typeparam>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection ConfigureTemporalActivities<T>(
        this IServiceCollection services,
        string? specificTaskQueue = null) =>
        services.ConfigureTemporalActivities(typeof(T), specificTaskQueue);

    /// <summary>
    /// Configure all activity methods for the given type on <see cref="ActivityOptions" />.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="type">Activity class type.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    /// <returns>Collection for chaining.</returns>
    public static IServiceCollection ConfigureTemporalActivities(
        this IServiceCollection services,
        Type type,
        string? specificTaskQueue = null) =>
        services.Configure<ActivityOptions>(opts => opts.AddAllActivities(type, specificTaskQueue));
}