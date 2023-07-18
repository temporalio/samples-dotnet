namespace TemporalioSamples.DependencyInjection;

public interface IMyDatabaseClient
{
    Task<string> SelectValueAsync(string table);
}