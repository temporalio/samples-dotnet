using System.Diagnostics.CodeAnalysis;
using Temporalio.Activities;

namespace TemporalioSamples.Batch.SlidingWindow;

/// <summary>
/// Fake activities used to iterate over a list of records. The real application would iterate
/// over an existing dataset or file.
/// </summary>
public static class RecordLoaderActivities
{
    private const int TotalCount = 300;

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
        if (offset < TotalCount)
        {
            for (var i = offset; i < Math.Min(offset + pageSize, TotalCount); i++)
            {
                records.Add(new SingleRecord(i));
            }
        }

        return records;
    }

    /// <summary>
    /// Returns the total record count.
    ///
    /// <para>Used to divide record ranges among partitions. Some applications might choose a
    /// completely different approach for partitioning the dataset.</para>
    /// </summary>
    [Activity]
    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Activities must be methods.")]
    public static int GetRecordCount() => TotalCount;
}
