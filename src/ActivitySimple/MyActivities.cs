namespace TemporalioSamples.ActivitySimple;

using Temporalio.Activities;

public class MyActivities
{
    private readonly MyDatabaseClient dbClient = new();

    // Activities can be static and/or sync
    [Activity]
    public static string DoStaticThing() => "some-static-value";

    // Activities can be methods that can access state
    [Activity]
    public Task<string> SelectFromDatabaseAsync(string table) =>
        dbClient.SelectValueAsync(table);

    public class MyDatabaseClient
    {
        public Task<string> SelectValueAsync(string table) =>
            Task.FromResult($"some-db-value from table {table}");
    }
}