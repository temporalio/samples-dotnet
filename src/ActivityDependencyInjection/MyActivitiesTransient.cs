namespace TemporalioSamples.ActivityDependencyInjection;

using Temporalio.Activities;

/// <summary>
/// Activity class that can be registered as transient.
/// </summary>
public class MyActivitiesTransient
{
    private readonly IMyDatabaseClient dbClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyActivitiesTransient"/> class.
    /// </summary>
    /// <param name="dbClient">DB client.</param>
    public MyActivitiesTransient(IMyDatabaseClient dbClient) => this.dbClient = dbClient;

    /// <summary>
    /// Activity for demonstration.
    /// </summary>
    /// <returns>Task for completion.</returns>
    [Activity]
    public async Task DoTransientDatabaseStuffAsync()
    {
        Console.WriteLine("Transient activity DB call: {0}", await dbClient.SelectValueAsync("some-db-table"));
    }
}