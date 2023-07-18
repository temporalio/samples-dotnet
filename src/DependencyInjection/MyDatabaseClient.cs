namespace TemporalioSamples.DependencyInjection;

public class MyDatabaseClient : IMyDatabaseClient
{
    public Task<string> SelectValueAsync(string table) =>
        Task.FromResult($"some-db-value from table {table}");
}