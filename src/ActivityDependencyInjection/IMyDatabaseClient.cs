namespace TemporalioSamples.ActivityDependencyInjection;

/// <summary>
/// Database client just for demonstration.
/// </summary>
public interface IMyDatabaseClient
{
    /// <summary>
    /// Select a value.
    /// </summary>
    /// <param name="table">Table to select from.</param>
    /// <returns>Task for with value.</returns>
    public Task<string> SelectValueAsync(string table);

    /// <summary>
    /// Implementation of database client.
    /// </summary>
    public class Core : IMyDatabaseClient
    {
        /// <inheritdoc />
        public Task<string> SelectValueAsync(string table) =>
            Task.FromResult($"some-db-value from table {table}");
    }
}