namespace TemporalioSamples.ActivityDependencyInjection;

using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;
using Temporalio.Worker;

/// <summary>
/// Options that can be configured with activity details. This is usually injected via
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}" /> by workers to call
/// <see cref="ApplyToWorkerOptions" /> to bind the activities.
/// </summary>
public class ActivityCollection : List<ActivityCollection.ActivityDetails>
{
    /// <summary>
    /// Add all activities on the given type.
    /// </summary>
    /// <typeparam name="T">Activity class type.</typeparam>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    public void AddAllActivities<T>(string? specificTaskQueue = null) => AddAllActivities(typeof(T), specificTaskQueue);

    /// <summary>
    /// Add all activity methods on the given type.
    /// </summary>
    /// <param name="type">Activity class type.</param>
    /// <param name="specificTaskQueue">Optional task queue to put activity on. Unset means all.</param>
    public void AddAllActivities(Type type, string? specificTaskQueue = null) =>
        AddRange(type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).
            Select(method =>
            {
                var attr = method.GetCustomAttribute<ActivityAttribute>(true);
                if (attr == null)
                {
                    return null;
                }
                // Make sure not already present
                var name = ActivityDefinition.NameFromMethod(method);
                if (this.Any(existing =>
                    existing.Name == name &&
                    (existing.SpecificTaskQueue == specificTaskQueue || existing.SpecificTaskQueue == null)))
                {
                    throw new InvalidOperationException($"Activity {name} already exists");
                }
                var parms = method.GetParameters();
                return new ActivityDetails(
                    Name: name,
                    ReturnType: method.ReturnType,
                    ParameterTypes: parms.Select(p => p.ParameterType).ToArray(),
                    RequiredParameterCount: parms.Count(p => !p.HasDefaultValue),
                    SpecificTaskQueue: specificTaskQueue,
                    InstanceType: method.IsStatic ? null : type,
                    Method: method);
            }).OfType<ActivityDetails>());

    /// <summary>
    /// Apply known activities to the given worker options.
    /// </summary>
    /// <param name="serviceProvider">Service provider to use to get activity instances.</param>
    /// <param name="options">Options to update.</param>
    public void ApplyToWorkerOptions(IServiceProvider serviceProvider, TemporalWorkerOptions options)
    {
        if (options.TaskQueue == null)
        {
            throw new InvalidOperationException("Options must have task queue before configure");
        }
        foreach (var activity in this)
        {
            options.AddActivity(activity.BuildDefinition(serviceProvider));
        }
    }

    /// <summary>
    /// Details for an activity that will be bound to worker options.
    /// </summary>
    /// <param name="Name">Activity name.</param>
    /// <param name="ReturnType">Activity return type.</param>
    /// <param name="ParameterTypes">Activity parameter types.</param>
    /// <param name="RequiredParameterCount">Activity required parameter count.</param>
    /// <param name="SpecificTaskQueue">Activity specific task queue if any..</param>
    /// <param name="InstanceType">Activity instance type if not static.</param>
    /// <param name="Method">Activity method to invoke.</param>
    public record ActivityDetails(
        string Name,
        Type ReturnType,
        IReadOnlyCollection<Type> ParameterTypes,
        int RequiredParameterCount,
        string? SpecificTaskQueue,
        Type? InstanceType,
        MethodInfo Method)
    {
        /// <summary>
        /// Build an activity definition using the given service provider.
        /// </summary>
        /// <param name="serviceProvider">Service provider for creating activity instance.</param>
        /// <returns>Created definition.</returns>
        public ActivityDefinition BuildDefinition(IServiceProvider serviceProvider)
        {
            // Invoker can be async
            async Task<object?> InvokeAsync(object?[] args)
            {
                // TODO(cretz): Much of this function is mimicked in ActivityDefinition.InvokeAsync
                // so we should just provide a create-instance helper.
                if (InstanceType == null)
                {
                    // Invoke static
                    try
                    {
                        return Method.Invoke(null, args);
                    }
                    catch (TargetInvocationException e)
                    {
                        ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                        // Unreachable
                        throw new InvalidOperationException("Unreachable");
                    }
                }
                // Use an async scope to properly handle IAsyncDisposable
                await using var scope = serviceProvider.CreateAsyncScope();
                object? result;
                try
                {
                    result = Method.Invoke(
                        scope.ServiceProvider.GetRequiredService(InstanceType), args);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                    // Unreachable
                    throw new InvalidOperationException("Unreachable");
                }
                // In order to make sure the scope lasts the life of the activity, we need to
                // wait on the task if it's a task
                if (result is Task resultTask)
                {
                    await resultTask.ConfigureAwait(false);
                    // We have to use reflection to extract value if it's a Task<>
                    var resultTaskType = resultTask.GetType();
                    if (resultTaskType.IsGenericType)
                    {
                        result = resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                    }
                    else
                    {
                        result = ValueTuple.Create();
                    }
                }
                return result;
            }
            return ActivityDefinition.Create(
                Name,
                ReturnType,
                ParameterTypes,
                RequiredParameterCount,
                InvokeAsync);
        }
    }
}