namespace TemporalioSamples.Tests.Batch.HeartbeatingActivity;

using Temporalio.Converters;
using Temporalio.Testing;
using TemporalioSamples.Batch.HeartbeatingActivity;
using Xunit;
using Xunit.Abstractions;

public class RecordProcessorActivitiesTests : TestBase
{
    public RecordProcessorActivitiesTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task ProcessRecordsAsync_HeartbeatDetailPresent_ResumesInsteadOfStartingOver()
    {
        // Seeds the activity context with heartbeat details as if this were a retry after a
        // worker restart that got as far as record 995 before dying.
        var heartbeats = new List<object?[]>();
        var env = new ActivityEnvironment
        {
            Info = ActivityEnvironment.DefaultInfo with
            {
                HeartbeatDetails = new[] { DataConverter.Default.PayloadConverter.ToPayload(995) },
            },
            Heartbeater = details => heartbeats.Add(details),
        };

        var result = await env.RunAsync(() => RecordProcessorActivities.ProcessRecordsAsync());

        Assert.Equal(1000, result);

        // Resumes from the seeded heartbeat detail rather than reprocessing records 0-994.
        Assert.Equal(5, heartbeats.Count);
        Assert.Equal(995, heartbeats[0][0]);
        Assert.Equal(999, heartbeats[^1][0]);
    }
}
