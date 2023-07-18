namespace TemporalioSamples.DependencyInjection;

using Temporalio.Activities;

public class MyActivities
{
    private readonly IMyDatabaseClient databaseClient;

    public MyActivities(IMyDatabaseClient databaseClient) => this.databaseClient = databaseClient;

    [Activity]
    public Task<string> SelectFromDatabaseAsync(string table) =>
        databaseClient.SelectValueAsync(table);
}