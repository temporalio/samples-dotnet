using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace TemporalioSamples.Batch.HeartbeatingActivity;

/// <summary>
/// Activity that processes a whole batch of records.
///
/// <para>It relies on a fake record loader to iterate over the set of records and process them
/// one by one. The heartbeat is used to remember the offset. On activity retry, the data from
/// the last recorded heartbeat is used to minimize the number of records that are reprocessed.
/// Note that not every heartbeat call is sent to the service; the frequency depends on the
/// heartbeat timeout the activity was scheduled with. If no heartbeat timeout is set, no
/// heartbeat is ever sent to the service.</para>
///
/// <para>The biggest advantage of this approach is efficiency: it uses very few Temporal
/// resources. The biggest limitation is that it cannot deal with individual record processing
/// failures. The only options are either failing the whole batch or skipping the record. While
/// it is possible to build additional logic to record failed records somewhere, the experience
/// is not seamless.</para>
/// </summary>
public static class RecordProcessorActivities
{
    // The sample always has 1000 records. The real application would iterate over an existing
    // dataset or file.
    private const int RecordCount = 1000;

    /// <summary>
    /// Processes all records in the dataset.
    /// </summary>
    /// <returns>The number of records processed.</returns>
    [Activity]
    public static async Task<int> ProcessRecordsAsync()
    {
        var context = ActivityExecutionContext.Current;

        // On activity retry, load the last reported offset from the heartbeat details.
        var offset = context.Info.HeartbeatDetails.Count > 0
            ? await context.Info.HeartbeatDetailAtAsync<int>(0)
            : 0;
        context.Logger.LogInformation("Activity ProcessRecordsAsync started with offset={Offset}", offset);

        // This sample implementation processes records one by one. If needed, it can be changed
        // to use a pool of tasks to process multiple records in parallel.
        while (true)
        {
            var record = GetRecord(offset);
            if (record is null)
            {
                return offset;
            }

            await ProcessRecordAsync(record, context.CancellationToken);

            // Report that the activity is still alive. The assumption is that each record takes
            // less time to process than the heartbeat timeout. Leverage heartbeat details to
            // record the offset.
            context.Heartbeat(offset);
            offset++;
        }
    }

    /// <summary>
    /// Returns the record at the given offset, or <c>null</c> if the offset exceeds the dataset
    /// size.
    /// </summary>
    private static SingleRecord? GetRecord(int offset) =>
        offset < RecordCount ? new SingleRecord(offset) : null;

    /// <summary>
    /// Fake record processing logic.
    /// </summary>
    private static async Task ProcessRecordAsync(SingleRecord record, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        ActivityExecutionContext.Current.Logger.LogInformation("Processed {Record}", record);
    }
}
