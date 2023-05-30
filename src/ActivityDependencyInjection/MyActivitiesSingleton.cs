namespace TemporalioSamples.ActivityDependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;

/// <summary>
/// Activity class that can be registered as a singleton.
/// </summary>
public class MyActivitiesSingleton
{
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyActivitiesSingleton"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider.</param>
    public MyActivitiesSingleton(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

    /// <summary>
    /// Activity for demonstration.
    /// </summary>
    /// <returns>Task for completion.</returns>
    [Activity]
    public async Task DoSingletonDatabaseStuffAsync()
    {
        var dbClient = serviceProvider.GetRequiredService<IMyDatabaseClient>();
        Console.WriteLine("Singleton activity DB call: {0}", await dbClient.SelectValueAsync("some-db-table"));
    }
}