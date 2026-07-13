using Temporalio.Activities;

namespace TemporalioSamples.Batch.Iterator;

/// <summary>
/// Activity used to iterate over a list of records.
///
/// <para>A specific implementation depends on a use case. For example, it can execute a SQL DB
/// query or read a comma delimited file. More complex use cases would need passing a different
/// type of offset parameter.</para>
/// </summary>
public static class RecordLoaderActivities
{
    // The sample always returns 5 pages. The real application would iterate over an existing
    // dataset or file.
    private const int PageCount = 5;

    /// <summary>
    /// Returns the next page of records.
    /// </summary>
    /// <param name="pageSize">Maximum number of records to return.</param>
    /// <param name="offset">Offset of the next page.</param>
    /// <returns>Empty list if there are no more records to process.</returns>
    [Activity]
    public static IReadOnlyList<SingleRecord> GetRecords(int pageSize, int offset)
    {
        var records = new List<SingleRecord>(pageSize);
        if (offset < pageSize * PageCount)
        {
            for (var i = 0; i < pageSize; i++)
            {
                records.Add(new SingleRecord(offset + i));
            }
        }

        return records;
    }
}
